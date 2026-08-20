using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using MiniApy.Api.Caching;
using MiniApy.Api.Data;
using MiniApy.Api.DTOs.Refunds;
using MiniApy.Api.Entities;
using MiniApy.Api.Enums;
using MiniApy.Api.Exceptions;
using MiniApy.Api.Helpers;
using MiniApy.Api.Interfaces;

namespace MiniApy.Api.Services;

public sealed class RefundService(
    AppDbContext dbContext,
    IWebhookService webhookService,
    HybridCache cache,
    ILogger<RefundService> logger)
    : IRefundService
{
    public async Task<RefundResponse> CreateAsync(
        Guid paymentId,
        CreateRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var databaseTransaction =
            await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var payment = await dbContext.Payments
            .FirstOrDefaultAsync(
                item => item.Id == paymentId,
                cancellationToken);

        if (payment is null)
        {
            throw new ResourceNotFoundException(
                $"Payment '{paymentId}' was not found.");
        }

        if (payment.Status == PaymentStatuses.REFUNDED)
        {
            throw new BusinessRuleException(
                "The payment has already been fully refunded.");
        }

        if (payment.Status != PaymentStatuses.COMPLETED)
        {
            throw new BusinessRuleException(
                "Only completed payments can be refunded.");
        }

        var completedRefundAmount = await dbContext.Refunds
            .Where(refund =>
                refund.PaymentId == paymentId &&
                refund.Status == RefundStatus.COMPLETED)
            .SumAsync(
                refund => (decimal?)refund.Amount,
                cancellationToken)
            ?? 0m;

        var reservedRefundAmount = await dbContext.Refunds
            .Where(refund =>
                refund.PaymentId == paymentId &&
                (
                    refund.Status == RefundStatus.PENDING ||
                    refund.Status == RefundStatus.PROCESSING
                ))
            .SumAsync(
                refund => (decimal?)refund.Amount,
                cancellationToken)
            ?? 0m;

        var availableRefundAmount =
            payment.Amount -
            completedRefundAmount -
            reservedRefundAmount;

        if (request.Amount > availableRefundAmount)
        {
            throw new BusinessRuleException(
                $"Refund amount exceeds the available refundable " +
                $"balance of {availableRefundAmount:F2} " +
                $"{payment.Currency}.");
        }

        var now = DateTimeOffset.UtcNow;

        var refund = new Refund
        {
            PaymentId = payment.Id,
            Reference = GenerateRefundReference(),
            Amount = request.Amount,
            Currency = payment.Currency,
            Reason = request.Reason.Trim(),
            Status = RefundStatus.PENDING,
            CreatedAt = now,
            UpdatedAt = now
        };

        var refundTransaction = new Entities.Transaction
        {
            PaymentId = payment.Id,
            Reference = GenerateRefundTransactionReference(),
            Type = TransactionType.REFUND,
            Status = TransactionStatus.PENDING,
            Amount = refund.Amount,
            Currency = refund.Currency,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Refunds.Add(refund);
        dbContext.Transactions.Add(refundTransaction);

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.RemoveByTagAsync(
    CacheKeys.MerchantPaymentsTag(merchantId: payment.MerchantId),
    cancellationToken);

        refund.Status = RefundStatus.PROCESSING;
        refund.UpdatedAt = DateTimeOffset.UtcNow;

        refundTransaction.Status = TransactionStatus.PROCESSING;
        refundTransaction.UpdatedAt = refund.UpdatedAt;

        await dbContext.SaveChangesAsync(cancellationToken);

        var completedAt = DateTimeOffset.UtcNow;

        refund.Status = RefundStatus.COMPLETED;
        refund.CompletedAt = completedAt;
        refund.UpdatedAt = completedAt;

        refundTransaction.Status = TransactionStatus.COMPLETED;
        refundTransaction.ProviderReference =
            GenerateProviderReference();
        refundTransaction.CompletedAt = completedAt;
        refundTransaction.UpdatedAt = completedAt;

        var totalCompletedRefundAmount =
            completedRefundAmount +
            refund.Amount;

        if (totalCompletedRefundAmount == payment.Amount)
        {
            payment.Status = PaymentStatuses.REFUNDED;
            payment.UpdatedAt = completedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.RemoveByTagAsync(
    CacheKeys.MerchantPaymentsTag(merchantId: payment.MerchantId),
    cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);

        await webhookService.QueuePaymentEventAsync(
            payment.Id,
            "refund.completed",
            CancellationToken.None);

        logger.LogInformation(
            "Completed refund {RefundId} for payment {PaymentId}; " +
            "payment status is {PaymentStatuses}",
            refund.Id,
            payment.Id,
            payment.Status);

        return refund.ToResponse();
    }

    public async Task<RefundResponse> GetByIdAsync(
        Guid refundId,
        CancellationToken cancellationToken = default)
    {
        var refund = await dbContext.Refunds
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == refundId,
                cancellationToken);

        if (refund is null)
        {
            throw new ResourceNotFoundException(
                $"Refund '{refundId}' was not found.");
        }

        return refund.ToResponse();
    }

    private static string GenerateRefundReference()
    {
        return $"REF-{Guid.NewGuid():N}".ToUpperInvariant();
    }

    private static string GenerateRefundTransactionReference()
    {
        return $"RTX-{Guid.NewGuid():N}".ToUpperInvariant();
    }

    private static string GenerateProviderReference()
    {
        return $"BANK-REF-{Guid.NewGuid():N}".ToUpperInvariant();
    }
}