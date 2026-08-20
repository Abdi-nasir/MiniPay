using MiniApy.Api.Enums;
namespace MiniApy.Api.DTOs.Settlements;

public sealed record SettlementResponse(
    Guid Id,
    Guid MerchantId,
    string Reference,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    decimal GrossAmount,
    decimal RefundAmount,
    decimal FeePercentage,
    decimal FeeAmount,
    decimal NetAmount,
    string Currency,
    SettlementStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);