using MiniApy.Api.Enums;
namespace MiniApy.Api.Entities;

public sealed class WebhookEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MerchantId { get; set; }

    public Merchant Merchant { get; set; } = null!;

    public Guid? PaymentId { get; set; }

    public Payment? Payment { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string TargetUrl { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public WebhookStatus Status { get; set; } = WebhookStatus.PENDING;

    public int AttemptCount { get; set; }

    public int? LastResponseStatusCode { get; set; }

    public string? LastResponseBody { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastAttemptAt { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }
}