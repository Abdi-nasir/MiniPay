using MiniApy.Api.DTOs.Merchants;
using MiniApy.Api.DTOs.Payments;
using MiniApy.Api.DTOs.Refunds;
using MiniApy.Api.DTOs.Settlements;
using MiniApy.Api.DTOs.Transactions;
using MiniApy.Api.Entities;

namespace MiniApy.Api.Helpers;

public static class DtoMapper
{
    public static MerchantResponse ToResponse(this Merchant merchant)
    {
        return new MerchantResponse(
            merchant.Id,
            merchant.Name,
            merchant.Email,
            merchant.WebhookUrl,
            merchant.IsActive,
            merchant.CreatedAt);
    }

    public static PaymentResponse ToResponse(this Payment payment)
    {
        return new PaymentResponse(
            payment.Id,
            payment.MerchantId,
            payment.Reference,
            payment.Amount,
            payment.Currency,
            payment.Description,
            payment.Status,
            payment.FailureReason,
            payment.CreatedAt,
            payment.UpdatedAt,
            payment.CompletedAt);
    }

    public static TransactionResponse ToResponse(
        this Entities.Transaction transaction)
    {
        return new TransactionResponse(
            transaction.Id,
            transaction.PaymentId,
            transaction.Reference,
            transaction.ProviderReference,
            transaction.Type,
            transaction.Status,
            transaction.Amount,
            transaction.Currency,
            transaction.FailureReason,
            transaction.CreatedAt,
            transaction.UpdatedAt,
            transaction.CompletedAt);
    }

    public static RefundResponse ToResponse(this Refund refund)
    {
        return new RefundResponse(
            refund.Id,
            refund.PaymentId,
            refund.Reference,
            refund.Amount,
            refund.Currency,
            refund.Reason,
            refund.Status,
            refund.FailureReason,
            refund.CreatedAt,
            refund.UpdatedAt,
            refund.CompletedAt);
    }

    public static SettlementResponse ToResponse(
        this Settlement settlement)
    {
        return new SettlementResponse(
            settlement.Id,
        settlement.MerchantId,
        settlement.Reference,
        settlement.PeriodStart,
        settlement.PeriodEnd,
        settlement.GrossAmount,
        settlement.RefundAmount,
        settlement.FeePercentage,
        settlement.FeeAmount,
        settlement.NetAmount,
        settlement.Currency,
        settlement.Status,
        settlement.CreatedAt,
        settlement.CompletedAt);
    }
}