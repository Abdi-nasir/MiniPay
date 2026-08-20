using MiniApy.Api.DTOs.Payments;

namespace MiniApy.Api.DTOs.Webhooks;

public sealed record WebhookPayload(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    PaymentResponse Payment);