using MiniApy.Api.DTOs.Refunds;

namespace MiniApy.Api.Interfaces;

public interface IRefundService
{
    Task<RefundResponse> CreateAsync(
        Guid paymentId,
        CreateRefundRequest request,
        CancellationToken cancellationToken = default);

    Task<RefundResponse> GetByIdAsync(
        Guid refundId,
        CancellationToken cancellationToken = default);
}