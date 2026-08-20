using MiniApy.Api.DTOs.Merchants;
namespace MiniApy.Api.Interfaces;

public interface IMerchantService
{
    Task<MerchantRegistrationResponse> RegisterAsync(
        MerchantRegistrationRequest request,
        CancellationToken cancellationToken = default);

    Task<MerchantResponse> GetByIdAsync(
        Guid merchantId,
        CancellationToken cancellationToken = default);

         Task<IReadOnlyList<MerchantResponse>> ListAsync(
        MerchantsQuery query,
        CancellationToken cancellationToken = default);
}