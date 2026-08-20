using MiniApy.Api.DTOs.Settlements;

namespace MiniApy.Api.Interfaces;

public interface ISettlementService
{

    Task<SettlementResponse> GenerateAsync(
        GenerateSettlementRequest request,
        CancellationToken cancellationToken = default);

    Task<SettlementResponse> GetByIdAsync(
        Guid settlementId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SettlementResponse>> ListAsync(
        SettlementQuery query,
        CancellationToken cancellationToken = default);
}