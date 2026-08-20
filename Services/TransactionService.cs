using Microsoft.EntityFrameworkCore;
using MiniApy.Api.Data;
using MiniApy.Api.DTOs.Transactions;
using MiniApy.Api.Helpers;
using MiniApy.Api.Interfaces;

namespace MiniApy.Api.Services;

public sealed class TransactionService(
    AppDbContext dbContext)
    : ITransactionService
{
    public async Task<IReadOnlyList<TransactionResponse>> ListAsync(
        TransactionQuery query,
        CancellationToken cancellationToken = default)
    {
        var transactions = dbContext.Transactions
            .AsNoTracking()
            .AsQueryable();

        if (query.MerchantId.HasValue)
        {
            transactions = transactions.Where(
                transaction =>
                    transaction.Payment.MerchantId ==
                    query.MerchantId.Value);
        }

        if (query.PaymentId.HasValue)
        {
            transactions = transactions.Where(
                transaction =>
                    transaction.PaymentId == query.PaymentId.Value);
        }

        if (query.Status.HasValue)
        {
            transactions = transactions.Where(
                transaction =>
                    transaction.Status == query.Status.Value);
        }

        if (query.Type.HasValue)
        {
            transactions = transactions.Where(
                transaction =>
                    transaction.Type == query.Type.Value);
        }

        var skip = (query.Page - 1) * query.PageSize;

        return await transactions
            .OrderByDescending(transaction => transaction.CreatedAt)
            .Skip(skip)
            .Take(query.PageSize)
            .Select(transaction => transaction.ToResponse())
            .ToListAsync(cancellationToken);
    }
}