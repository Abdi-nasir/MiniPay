using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MiniApy.Api.Data;
using MiniApy.Api.DTOs.Webhooks;
using MiniApy.Api.Entities;
using MiniApy.Api.Enums;
using MiniApy.Api.Exceptions;
using MiniApy.Api.Helpers;
using MiniApy.Api.Interfaces;

namespace MiniApy.Api.Services;

public sealed class WebhookService(
    AppDbContext dbContext,
    ILogger<WebhookService> logger)
    : IWebhookService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public async Task<Guid?> QueuePaymentEventAsync(
        Guid paymentId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        var payment = await dbContext.Payments
            .Include(item => item.Merchant)
            .FirstOrDefaultAsync(
                item => item.Id == paymentId,
                cancellationToken);

        if (payment is null)
        {
            throw new ResourceNotFoundException(
                $"Payment '{paymentId}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(payment.Merchant.WebhookUrl))
        {
            logger.LogDebug(
                "Merchant {MerchantId} has no webhook URL; " +
                "event {EventType} was not queued",
                payment.MerchantId,
                eventType);

            return null;
        }

        var eventId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var payload = new WebhookPayload(
            eventId,
            eventType,
            now,
            payment.ToResponse());

        var webhookEvent = new WebhookEvent
        {
            Id = eventId,
            MerchantId = payment.MerchantId,
            PaymentId = payment.Id,
            EventType = eventType,
            TargetUrl = payment.Merchant.WebhookUrl,
            PayloadJson = JsonSerializer.Serialize(
                payload,
                JsonOptions),
            Status = WebhookStatus.PENDING,
            AttemptCount = 0,
            CreatedAt = now,
            NextAttemptAt = now
        };

        dbContext.WebhookEvents.Add(webhookEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Queued webhook event {WebhookEventId} " +
            "of type {EventType} for payment {PaymentId}",
            webhookEvent.Id,
            webhookEvent.EventType,
            payment.Id);

        return webhookEvent.Id;
    }
}