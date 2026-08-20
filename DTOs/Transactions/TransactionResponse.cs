using MiniApy.Api.Enums;
namespace MiniApy.Api.DTOs.Transactions;

public sealed record TransactionResponse(
    Guid Id,
    Guid PaymentId,
    string Reference,
    string? ProviderReference,
    TransactionType Type,
    TransactionStatus Status,
    decimal Amount,
    string Currency,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);