using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniApy.Api.Data;
using MiniApy.Api.Enums;
using MiniApy.Api.Helpers;
using MiniApy.Api.Options;

namespace MiniApy.Api.Services;

public sealed class WebhookDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IOptions<WebhookOptions> options,
    ILogger<WebhookDeliveryWorker> logger)
    : BackgroundService
{
    private readonly WebhookOptions _options = options.Value;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Webhook delivery worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Unhandled error in webhook delivery worker");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(
                        _options.PollIntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation(
            "Webhook delivery worker stopped");
    }

    private async Task ProcessBatchAsync(
        CancellationToken cancellationToken)
    {
        await RecoverStaleClaimsAsync(cancellationToken);

        await using var scope = scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;

        var webhookEventIds = await dbContext.WebhookEvents
            .AsNoTracking()
            .Where(item =>
                (
                    item.Status == WebhookStatus.PENDING ||
                    item.Status == WebhookStatus.FAILED
                ) &&
                item.AttemptCount < _options.MaximumAttempts &&
                (
                    item.NextAttemptAt == null ||
                    item.NextAttemptAt <= now
                ))
            .OrderBy(item => item.CreatedAt)
            .Select(item => item.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var webhookEventId in webhookEventIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await DeliverAsync(
                webhookEventId,
                cancellationToken);
        }
    }

    private async Task DeliverAsync(
        Guid webhookEventId,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var attemptTime = DateTimeOffset.UtcNow;

        var claimedRows = await dbContext.WebhookEvents
            .Where(item =>
                item.Id == webhookEventId &&
                (
                    item.Status == WebhookStatus.PENDING ||
                    item.Status == WebhookStatus.FAILED
                ) &&
                item.AttemptCount < _options.MaximumAttempts &&
                (
                    item.NextAttemptAt == null ||
                    item.NextAttemptAt <= attemptTime
                ))
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(
                        item => item.Status,
                        WebhookStatus.PROCESSING)
                    .SetProperty(
                        item => item.AttemptCount,
                        item => item.AttemptCount + 1)
                    .SetProperty(
                        item => item.LastAttemptAt,
                        attemptTime)
                    .SetProperty(
                        item => item.NextAttemptAt,
                        (DateTimeOffset?)null),
                cancellationToken);

        if (claimedRows == 0)
        {
            return;
        }

        var webhookEvent = await dbContext.WebhookEvents
            .FirstAsync(
                item => item.Id == webhookEventId,
                cancellationToken);

        try
        {
            using var request = CreateRequest(webhookEvent);

            var client = httpClientFactory.CreateClient(
                "MerchantWebhooks");

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            var responseBody = await ReadLimitedResponseBodyAsync(
                response,
                cancellationToken);

            webhookEvent.LastResponseStatusCode =
                (int)response.StatusCode;

            webhookEvent.LastResponseBody = responseBody;
            webhookEvent.LastError = null;

            if (response.IsSuccessStatusCode)
            {
                MarkDelivered(
                    webhookEvent,
                    DateTimeOffset.UtcNow);

                logger.LogInformation(
                    "Delivered webhook {WebhookEventId} " +
                    "to {TargetUrl} on attempt {AttemptCount} " +
                    "with status code {StatusCode}",
                    webhookEvent.Id,
                    webhookEvent.TargetUrl,
                    webhookEvent.AttemptCount,
                    (int)response.StatusCode);
            }
            else
            {
                MarkFailed(
                    webhookEvent,
                    $"Merchant returned HTTP " +
                    $"{(int)response.StatusCode} " +
                    $"{response.StatusCode}");

                logger.LogWarning(
                    "Webhook {WebhookEventId} to {TargetUrl} " +
                    "failed on attempt {AttemptCount} " +
                    "with HTTP status {StatusCode}",
                    webhookEvent.Id,
                    webhookEvent.TargetUrl,
                    webhookEvent.AttemptCount,
                    (int)response.StatusCode);
            }
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            MarkFailed(
                webhookEvent,
                "Webhook request timed out.");

            logger.LogWarning(
                "Webhook {WebhookEventId} timed out " +
                "on attempt {AttemptCount}",
                webhookEvent.Id,
                webhookEvent.AttemptCount);
        }
        catch (HttpRequestException exception)
        {
            MarkFailed(
                webhookEvent,
                Truncate(exception.Message, 2_000));

            logger.LogWarning(
                exception,
                "HTTP failure delivering webhook " +
                "{WebhookEventId} on attempt {AttemptCount}",
                webhookEvent.Id,
                webhookEvent.AttemptCount);
        }
        catch (Exception exception)
        {
            MarkFailed(
                webhookEvent,
                Truncate(exception.Message, 2_000));

            logger.LogError(
                exception,
                "Unexpected failure delivering webhook " +
                "{WebhookEventId} on attempt {AttemptCount}",
                webhookEvent.Id,
                webhookEvent.AttemptCount);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private HttpRequestMessage CreateRequest(
        Entities.WebhookEvent webhookEvent)
    {
        var timestamp = DateTimeOffset.UtcNow
            .ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        var signature = WebhookSignature.Create(
            _options.SigningSecret,
            timestamp,
            webhookEvent.PayloadJson);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            webhookEvent.TargetUrl);

        request.Content = new StringContent(
            webhookEvent.PayloadJson,
            Encoding.UTF8,
            "application/json");

        request.Headers.Add(
            "X-MiniApy-Event-Id",
            webhookEvent.Id.ToString());

        request.Headers.Add(
            "X-MiniApy-Event-Type",
            webhookEvent.EventType);

        request.Headers.Add(
            "X-MiniApy-Timestamp",
            timestamp);

        request.Headers.Add(
            "X-MiniApy-Signature",
            signature);

        return request;
    }

    private void MarkDelivered(
        Entities.WebhookEvent webhookEvent,
        DateTimeOffset deliveredAt)
    {
        webhookEvent.Status = WebhookStatus.DELIVERED;
        webhookEvent.DeliveredAt = deliveredAt;
        webhookEvent.NextAttemptAt = null;
    }

    private void MarkFailed(
        Entities.WebhookEvent webhookEvent,
        string error)
    {
        webhookEvent.Status = WebhookStatus.FAILED;
        webhookEvent.LastError = Truncate(error, 2_000);

        webhookEvent.NextAttemptAt =
            webhookEvent.AttemptCount >= _options.MaximumAttempts
                ? null
                : DateTimeOffset.UtcNow.Add(
                    CalculateRetryDelay(
                        webhookEvent.AttemptCount));
    }

    private TimeSpan CalculateRetryDelay(int attemptCount)
    {
        var exponent = Math.Max(0, attemptCount - 1);

        var multiplier = Math.Pow(2, exponent);

        var delaySeconds =
            _options.InitialRetryDelaySeconds *
            multiplier;

        var cappedDelaySeconds = Math.Min(
            delaySeconds,
            3_600);

        return TimeSpan.FromSeconds(cappedDelaySeconds);
    }

    private async Task RecoverStaleClaimsAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var staleBefore = DateTimeOffset.UtcNow.AddSeconds(
            -Math.Max(_options.TimeoutSeconds * 2, 30));

        var recoveredCount = await dbContext.WebhookEvents
            .Where(item =>
                item.Status == WebhookStatus.PROCESSING &&
                item.LastAttemptAt != null &&
                item.LastAttemptAt < staleBefore)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(
                        item => item.Status,
                        WebhookStatus.FAILED)
                    .SetProperty(
                        item => item.LastError,
                        "Recovered stale delivery claim.")
                    .SetProperty(
                        item => item.NextAttemptAt,
                        DateTimeOffset.UtcNow),
                cancellationToken);

        if (recoveredCount > 0)
        {
            logger.LogWarning(
                "Recovered {RecoveredCount} stale webhook claims",
                recoveredCount);
        }
    }

    private static async Task<string?> ReadLimitedResponseBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var responseBody = await response.Content.ReadAsStringAsync(
            cancellationToken);

        return string.IsNullOrEmpty(responseBody)
            ? null
            : Truncate(responseBody, 10_000);
    }

    private static string Truncate(
        string value,
        int maximumLength)
    {
        return value.Length <= maximumLength
            ? value
            : value[..maximumLength];
    }
}