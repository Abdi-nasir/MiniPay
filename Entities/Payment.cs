
using MiniApy.Api.Enums;

namespace MiniApy.Api.Entities;

public sealed class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MerchantId { get; set; }

    public Merchant Merchant { get; set; } = null!;

    public string Reference { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string? Description { get; set; }

    public PaymentStatuses Status { get; set; } = PaymentStatuses.CREATED;

    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public ICollection<Transaction> Transactions { get; set; } =
        new List<Transaction>();

    public ICollection<Refund> Refunds { get; set; } =
        new List<Refund>();

    public ICollection<WebhookEvent> WebhookEvents { get; set; } =
        new List<WebhookEvent>();
}