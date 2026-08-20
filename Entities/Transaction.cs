using MiniApy.Api.Enums;
namespace MiniApy.Api.Entities;

public sealed class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentId { get; set; }

    public Payment Payment { get; set; } = null!;
    public Guid? SettlementId { get; set; }
    public Settlement? Settlement { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string? ProviderReference { get; set; }

    public TransactionType Type { get; set; } = TransactionType.PAYMENT;

    public TransactionStatus Status { get; set; } =
        TransactionStatus.PENDING;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }
}