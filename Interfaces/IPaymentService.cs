
using MiniApy.Api.DTOs.Payments;

namespace MiniApy.Api.Interfaces;

public interface IPaymentService
{
    Task<PaymentResponse> CreateAsync(
        Guid merchantId,
        string idempotencyKey,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentResponse> GetByIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default);

        Task<PaymentResponse> ConfirmAsync(
        Guid paymentId,
        ConfirmPaymentRequest request,
        CancellationToken cancellationToken = default);


        Task<PaymentListResponse> GetByMerchantAsync(
    Guid merchantId,
    int page,
    int pageSize,
    CancellationToken cancellationToken = default);
}