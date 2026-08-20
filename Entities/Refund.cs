using MiniApy.Api.Enums;

namespace MiniApy.Api.Entities;

public sealed class Refund
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentId { get; set; }

    public Payment Payment { get; set; } = null!;

    public string Reference { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public RefundStatus Status { get; set; } = RefundStatus.PENDING;

    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }
}