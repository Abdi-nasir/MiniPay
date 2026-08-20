


namespace MiniApy.Api.DTOs.Payments;

public sealed record PaymentListResponse(
    Guid MerchantId,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<PaymentResponse> Payments
);