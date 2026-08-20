using MiniApy.Api.Enums;

namespace MiniApy.Api.DTOs.Refunds;

public sealed record RefundResponse(
    Guid Id,
    Guid PaymentId,
    string Reference,
    decimal Amount,
    string Currency,
    string Reason,
    RefundStatus Status,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);