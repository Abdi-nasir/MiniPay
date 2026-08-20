using MiniApy.Api.Enums;
namespace MiniApy.Api.Entities;

public sealed class Settlement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MerchantId { get; set; }

    public Merchant Merchant { get; set; } = null!;

    public string Reference { get; set; } = string.Empty;

    public DateTimeOffset PeriodStart { get; set; }

    public DateTimeOffset PeriodEnd { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal RefundAmount { get; set; }

    public decimal FeeAmount { get; set; }

    public decimal NetAmount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public decimal FeePercentage { get; set; }

    public SettlementStatus Status { get; set; } =
        SettlementStatus.PENDING;

    public DateTimeOffset CreatedAt { get; set; } =
        DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public ICollection<Transaction> Transactions { get; set; } =
        [];
}