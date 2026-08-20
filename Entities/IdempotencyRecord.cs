namespace MiniApy.Api.Entities;

public sealed class IdempotencyRecord
{
    public Guid Id { get; set; }

    public Guid MerchantId { get; set; }

    public string Operation { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string RequestHash { get; set; } = string.Empty;

    public Guid ResourceId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}