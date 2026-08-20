using MiniApy.Api.DTOs.Transactions;

namespace MiniApy.Api.Interfaces;

public interface ITransactionService
{
   Task<IReadOnlyList<TransactionResponse>> ListAsync(
        TransactionQuery query,
        CancellationToken cancellationToken = default);
}