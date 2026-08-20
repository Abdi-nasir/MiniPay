using MiniApy.Api.Enums;
namespace MiniApy.Api.DTOs.Payments;

public sealed record PaymentResponse(
    Guid Id,
    Guid MerchantId,
    string Reference,
    decimal Amount,
    string Currency,
    string? Description,
    PaymentStatuses Status,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);