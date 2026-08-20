namespace MiniApy.Api.Entities;

public sealed class Merchant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string ApiKeyHash { get; set; } = string.Empty;

    public string? WebhookUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public ICollection<Settlement> Settlements { get; set; } =
        new List<Settlement>();

    public ICollection<WebhookEvent> WebhookEvents { get; set; } =
        new List<WebhookEvent>();
}