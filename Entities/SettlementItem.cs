using MiniApy.Api.Enums;
namespace MiniApy.Api.Entities;

public sealed class SettlementItem
{
    public Guid Id { get; set; }

    public Guid SettlementId { get; set; }

    public Settlement Settlement { get; set; } = null!;

    public Guid TransactionId { get; set; }

    public Entities.Transaction Transaction { get; set; } = null!;

    public TransactionType Type { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}