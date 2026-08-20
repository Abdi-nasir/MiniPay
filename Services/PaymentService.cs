using Microsoft.EntityFrameworkCore;
using MiniApy.Api.Entities;
using MiniApy.Api.Interfaces;
using MiniApy.Api.DTOs.Payments;
using MiniApy.Api.Exceptions;
using MiniApy.Api.Helpers;
using MiniApy.Api.Data;
using MiniApy.Api.Enums;
using Microsoft.Extensions.Caching.Hybrid;
using MiniApy.Api.Caching;
using Npgsql;

namespace MiniApy.Api.Services;

public sealed class PaymentService(
    AppDbContext dbContext,
    IWebhookService webhookService,
    HybridCache cache,
    IConfiguration configuration,

    ILogger<PaymentService> logger)
    : IPaymentService
{
public async Task<PaymentResponse> CreateAsync(
    Guid merchantId,
    string idempotencyKey,
    CreatePaymentRequest request,
    CancellationToken cancellationToken = default)
{
    const string operation = "payment:create";

    var normalizedKey =
        IdempotencyHelper.ValidateKey(idempotencyKey);

    var requestHash =
        IdempotencyHelper.HashPaymentRequest(
            merchantId,
            request);

    var existingRecord = await dbContext.IdempotencyRecords
        .AsNoTracking()
        .FirstOrDefaultAsync(
            item =>
                item.MerchantId == merchantId &&
                item.Operation == operation &&
                item.Key == normalizedKey,
            cancellationToken);

    if (existingRecord is not null)
    {
        return await ResolveExistingPaymentAsync(
            existingRecord,
            requestHash,
            cancellationToken);
    }

    await using var transaction =
        await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

    try
    {
        var merchant = await dbContext.Merchants
            .FirstOrDefaultAsync(
                item => item.Id == merchantId,
                cancellationToken);

        if (merchant is null)
        {
            throw new ResourceNotFoundException(
                $"Merchant '{merchantId}' was not found.");
        }

        if (!merchant.IsActive)
        {
            throw new BusinessRuleException(
                "The merchant is inactive and cannot create payments.");
        }

        var reference = request.Reference.Trim();

        var referenceAlreadyExists = await dbContext.Payments
            .AnyAsync(
                payment =>
                    payment.MerchantId == merchantId &&
                    payment.Reference == reference,
                cancellationToken);

        if (referenceAlreadyExists)
        {
            throw new ResourceConflictException(
                $"Payment reference '{reference}' already exists " +
                "for this merchant.");
        }

        var now = DateTimeOffset.UtcNow;
        var paymentId = Guid.NewGuid();

        var payment = new Payment
        {
            Id = paymentId,
            MerchantId = merchantId,
            Reference = reference,
            Amount = request.Amount,
            Currency = request.Currency
                .Trim()
                .ToUpperInvariant(),
            Description = NormalizeOptionalValue(
                request.Description),
            Status = PaymentStatuses.CREATED,
            CreatedAt = now,
            UpdatedAt = now
        };

        var idempotencyRecord = new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            Operation = operation,
            Key = normalizedKey,
            RequestHash = requestHash,
            ResourceId = paymentId,
            CreatedAt = now,
            ExpiresAt = now.AddHours(24)
        };

        dbContext.Payments.Add(payment);
        dbContext.IdempotencyRecords.Add(idempotencyRecord);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        await InvalidatePaymentCacheAsync(merchantId);

        logger.LogInformation(
            "Created payment {PaymentId} for merchant " +
            "{MerchantId} using idempotency key {IdempotencyKey}",
            payment.Id,
            merchantId,
            normalizedKey);

        return payment.ToResponse();
    }
    catch (DbUpdateException exception)
        when (IsIdempotencyConflict(exception))
    {
        await transaction.RollbackAsync(
            CancellationToken.None);

        dbContext.ChangeTracker.Clear();

        var concurrentRecord =
            await dbContext.IdempotencyRecords
                .AsNoTracking()
                .FirstAsync(
                    item =>
                        item.MerchantId == merchantId &&
                        item.Operation == operation &&
                        item.Key == normalizedKey,
                    CancellationToken.None);

        logger.LogInformation(
            "Concurrent idempotent payment request detected. " +
            "MerchantId: {MerchantId}, Key: {IdempotencyKey}",
            merchantId,
            normalizedKey);

        return await ResolveExistingPaymentAsync(
            concurrentRecord,
            requestHash,
            CancellationToken.None);
    }
}
    public async Task<PaymentResponse> GetByIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == paymentId,
                cancellationToken);

        if (payment is null)
        {
            throw new ResourceNotFoundException(
                $"Payment '{paymentId}' was not found.");
        }

        return payment.ToResponse();
    }

    public async Task<PaymentResponse> ConfirmAsync(
        Guid paymentId,
        ConfirmPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var existingPayment = await dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == paymentId,
                cancellationToken);

        if (existingPayment is null)
        {
            throw new ResourceNotFoundException(
                $"Payment '{paymentId}' was not found.");
        }

        if (existingPayment.Status == PaymentStatuses.COMPLETED)
        {
            logger.LogInformation(
                "Payment {PaymentId} was already completed; " +
                "returning the existing result",
                paymentId);

            return existingPayment.ToResponse();
        }

        if (existingPayment.Status != PaymentStatuses.CREATED)
        {
            throw new BusinessRuleException(
                $"Payment cannot be confirmed from status " +
                $"'{existingPayment.Status}'.");
        }

        var now = DateTimeOffset.UtcNow;

        var claimedRows = await dbContext.Payments
            .Where(item =>
                item.Id == paymentId &&
                item.Status == PaymentStatuses.CREATED)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(
                        item => item.Status,
                        PaymentStatuses.PENDING)
                    .SetProperty(
                        item => item.UpdatedAt,
                        now),
                cancellationToken);

        if (claimedRows == 0)
        {
            throw new ResourceConflictException(
                "The payment is already being processed by another request.");
        }

        var transaction = new Entities.Transaction
        {
            PaymentId = paymentId,
            Reference = GenerateTransactionReference(),
            Type = TransactionType.PAYMENT,
            Status = TransactionStatus.PENDING,
            Amount = existingPayment.Amount,
            Currency = existingPayment.Currency,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePaymentCacheAsync(merchantId: existingPayment.MerchantId);

        logger.LogInformation(
            "Payment {PaymentId} entered PENDING with transaction " +
            "{TransactionId}",
            paymentId,
            transaction.Id);


        var processingCancellationToken = CancellationToken.None;

        await Task.Delay(
            TimeSpan.FromMilliseconds(300),
            processingCancellationToken);

        var payment = await dbContext.Payments
            .FirstAsync(
                item => item.Id == paymentId,
                processingCancellationToken);

        payment.Status = PaymentStatuses.PROCESSING;
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        transaction.Status = TransactionStatus.PROCESSING;
        transaction.UpdatedAt = payment.UpdatedAt;

        await dbContext.SaveChangesAsync(processingCancellationToken);
        

        logger.LogInformation(
            "Payment {PaymentId} entered PROCESSING",
            paymentId);

        await Task.Delay(
            TimeSpan.FromMilliseconds(500),
            processingCancellationToken);

        var completedAt = DateTimeOffset.UtcNow;

        if (request.SimulateFailure)
        {
            CompleteAsFailed(
                payment,
                transaction,
                request.FailureReason,
                completedAt);
        }
        else
        {
            CompleteAsSuccessful(
                payment,
                transaction,
                completedAt);
        }

        await dbContext.SaveChangesAsync(processingCancellationToken);
        await InvalidatePaymentCacheAsync(merchantId: payment.MerchantId);

        var eventType = payment.Status == PaymentStatuses.COMPLETED
            ? "payment.completed"
            : "payment.failed";

        await webhookService.QueuePaymentEventAsync(
            payment.Id,
            eventType,
            processingCancellationToken);

        logger.LogInformation(
            "Payment {PaymentId} finished with status {PaymentStatus}",
            payment.Id,
            payment.Status);

        return payment.ToResponse();
    }

    public async Task<PaymentListResponse> GetByMerchantAsync(
    Guid merchantId,
    int page,
    int pageSize,
    CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var key = CacheKeys.PaymentList(merchantId, page, pageSize);

        var tag = CacheKeys.MerchantPaymentsTag(merchantId);

        var expirationSeconds = configuration.GetValue(
            "Caching:PaymentListExpirationSeconds",
            15);

        var localExpirationSeconds = configuration.GetValue(
            "Caching:PaymentListLocalExpirationSeconds",
            5);
logger.LogInformation(
    "CACHE LOOKUP: MerchantId: {MerchantId}, CacheKey: {CacheKey}",
    merchantId,
    key);
        return await cache.GetOrCreateAsync(
            key,
            async token =>
            {
                logger.LogInformation(
    "CACHE MISS: Loading payment list from PostgreSQL. " +
    "MerchantId: {MerchantId}, Page: {Page}, PageSize: {PageSize}, " +
    "CacheKey: {CacheKey}",
    merchantId,
    page,
    pageSize,
    key);

                return await GetByMerchantFromDatabaseAsync(
                    merchantId,
                    page,
                    pageSize,
                    token);
            },
            new HybridCacheEntryOptions
            {
                Expiration =
                    TimeSpan.FromSeconds(expirationSeconds),

                LocalCacheExpiration =
                    TimeSpan.FromSeconds(localExpirationSeconds)
            },
            tags: [tag],
            cancellationToken: cancellationToken);


    }


private static bool IsIdempotencyConflict(
    DbUpdateException exception)
{
    return exception.InnerException
        is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName:
                "ux_idempotency_merchant_operation_key"
        };
}

private async Task<PaymentResponse>
    ResolveExistingPaymentAsync(
        IdempotencyRecord record,
        string requestHash,
        CancellationToken cancellationToken)
{
    if (!string.Equals(
            record.RequestHash,
            requestHash,
            StringComparison.Ordinal))
    {
        throw new ResourceConflictException(
            "The Idempotency-Key was already used with " +
            "a different payment request.");
    }

    var payment = await dbContext.Payments
        .AsNoTracking()
        .FirstOrDefaultAsync(
            item => item.Id == record.ResourceId,
            cancellationToken);

    if (payment is null)
    {
        throw new InvalidOperationException(
            $"Idempotency record '{record.Id}' references " +
            $"missing payment '{record.ResourceId}'.");
    }

    logger.LogInformation(
        "Returning existing payment {PaymentId} for " +
        "idempotency key {IdempotencyKey}",
        payment.Id,
        record.Key);

    return payment.ToResponse();
}

private async Task InvalidatePaymentCacheAsync(
    Guid merchantId)
{
    var tag = CacheKeys.MerchantPaymentsTag(merchantId);

 
    await cache.RemoveByTagAsync(
        tag,
        CancellationToken.None);

    logger.LogInformation(
        "CACHE INVALIDATED: Merchant payment cache invalidated. " +
        "MerchantId: {MerchantId}, Tag: {CacheTag}",
        merchantId,
        tag);
}
    private static void CompleteAsSuccessful(
        Payment payment,
        Entities.Transaction transaction,
        DateTimeOffset completedAt)
    {
        payment.Status = PaymentStatuses.COMPLETED;
        payment.FailureReason = null;
        payment.CompletedAt = completedAt;
        payment.UpdatedAt = completedAt;

        transaction.Status = TransactionStatus.COMPLETED;
        transaction.ProviderReference =
            GenerateProviderReference();
        transaction.FailureReason = null;
        transaction.CompletedAt = completedAt;
        transaction.UpdatedAt = completedAt;
    }

    private static void CompleteAsFailed(
        Payment payment,
        Entities.Transaction transaction,
        string? requestedFailureReason,
        DateTimeOffset completedAt)
    {
        var failureReason = string.IsNullOrWhiteSpace(
            requestedFailureReason)
            ? "Simulated payment provider failure."
            : requestedFailureReason.Trim();

        payment.Status = PaymentStatuses.FAILED;
        payment.FailureReason = failureReason;
        payment.UpdatedAt = completedAt;

        transaction.Status = TransactionStatus.FAILED;
        transaction.FailureReason = failureReason;
        transaction.CompletedAt = completedAt;
        transaction.UpdatedAt = completedAt;
    }

    private async Task<PaymentListResponse>
    GetByMerchantFromDatabaseAsync(
        Guid merchantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var merchantExists = await dbContext.Merchants
            .AsNoTracking()
            .AnyAsync(
                merchant => merchant.Id == merchantId,
                cancellationToken);

        if (!merchantExists)
        {
            throw new ResourceNotFoundException(
                $"Merchant '{merchantId}' was not found.");
        }

        var query = dbContext.Payments
            .AsNoTracking()
            .Where(payment =>
                payment.MerchantId == merchantId);

        var totalCount =
            await query.CountAsync(cancellationToken);

        var payments = await query
            .OrderByDescending(payment => payment.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaymentListResponse(
            MerchantId: merchantId,
            Page: page,
            PageSize: pageSize,
            TotalCount: totalCount,
            Payments: [.. payments.Select(DtoMapper.ToResponse)]);
    }

    private static string GenerateTransactionReference()
    {
        return $"TXN-{Guid.NewGuid():N}".ToUpperInvariant();
    }

    private static string GenerateProviderReference()
    {
        return $"BANK-{Guid.NewGuid():N}".ToUpperInvariant();
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}