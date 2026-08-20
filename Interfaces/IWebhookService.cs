namespace MiniApy.Api.Interfaces;

public interface IWebhookService
{
    Task<Guid?> QueuePaymentEventAsync(
        Guid paymentId,
        string eventType,
        CancellationToken cancellationToken = default);
}