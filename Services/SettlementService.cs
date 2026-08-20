using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniApy.Api.Data;
using MiniApy.Api.DTOs.Settlements;
using MiniApy.Api.Entities;
using MiniApy.Api.Enums;
using MiniApy.Api.Exceptions;
using MiniApy.Api.Helpers;
using MiniApy.Api.Interfaces;
using MiniApy.Api.Options;

namespace MiniApy.Api.Services;

public sealed class SettlementService(
    AppDbContext dbContext,
    IOptions<SettlementOptions> options,
    ILogger<SettlementService> logger)
    : ISettlementService
{
    private readonly SettlementOptions _options =
        options.Value;

    public async Task<SettlementResponse> GenerateAsync(
        GenerateSettlementRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var currency = request.Currency
            .Trim()
            .ToUpperInvariant();

        await using var databaseTransaction =
            await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var merchant = await dbContext.Merchants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == request.MerchantId,
                cancellationToken);

        if (merchant is null)
        {
            throw new ResourceNotFoundException(
                $"Merchant '{request.MerchantId}' was not found.");
        }

        if (!merchant.IsActive)
        {
            throw new BusinessRuleException(
                "An inactive merchant cannot be settled.");
        }

        var transactions = await dbContext.Transactions
            .Where(transaction =>
                transaction.Payment.MerchantId ==
                    request.MerchantId &&
                transaction.Currency == currency &&
                transaction.Status ==
                    TransactionStatus.COMPLETED &&
                transaction.SettlementId == null &&
                transaction.CompletedAt != null &&
                transaction.CompletedAt >=
                    request.PeriodStart &&
                transaction.CompletedAt <
                    request.PeriodEnd)
            .OrderBy(transaction => transaction.CompletedAt)
            .ToListAsync(cancellationToken);

        if (transactions.Count == 0)
        {
            throw new BusinessRuleException(
                "No unsettled completed transactions were " +
                "found for the requested merchant, currency " +
                "and period.");
        }

        var grossAmount = transactions
            .Where(transaction =>
                transaction.Type ==
                    TransactionType.PAYMENT)
            .Sum(transaction => transaction.Amount);

        var refundAmount = transactions
            .Where(transaction =>
                transaction.Type ==
                    TransactionType.REFUND)
            .Sum(transaction => transaction.Amount);

        var feePercentage =
            _options.FeePercentage;

        var feeAmount = decimal.Round(
            grossAmount *
            feePercentage /
            100m,
            2,
            MidpointRounding.AwayFromZero);

        var netAmount =
            grossAmount -
            refundAmount -
            feeAmount;

        var now = DateTimeOffset.UtcNow;

        var settlement = new Settlement
        {
            MerchantId = merchant.Id,
            Reference = GenerateSettlementReference(),
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            GrossAmount = grossAmount,
            RefundAmount = refundAmount,
            FeePercentage = feePercentage,
            FeeAmount = feeAmount,
            NetAmount = netAmount,
            Currency = currency,
            Status = SettlementStatus.PENDING,
            CreatedAt = now
        };

        dbContext.Settlements.Add(settlement);

        foreach (var transaction in transactions)
        {
            transaction.Settlement = settlement;
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        settlement.Status =
            SettlementStatus.PROCESSING;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        if (_options.ProcessingDelayMilliseconds > 0)
        {
            await Task.Delay(
                _options.ProcessingDelayMilliseconds,
                cancellationToken);
        }

        settlement.Status =
            SettlementStatus.COMPLETED;

        settlement.CompletedAt =
            DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await databaseTransaction.CommitAsync(
            cancellationToken);

        logger.LogInformation(
            "Generated settlement {SettlementId} " +
            "for merchant {MerchantId}. " +
            "TransactionCount: {TransactionCount}, " +
            "GrossAmount: {GrossAmount}, " +
            "RefundAmount: {RefundAmount}, " +
            "FeePercentage: {FeePercentage}, " +
            "FeeAmount: {FeeAmount}, " +
            "NetAmount: {NetAmount}, " +
            "Currency: {Currency}",
            settlement.Id,
            settlement.MerchantId,
            transactions.Count,
            settlement.GrossAmount,
            settlement.RefundAmount,
            settlement.FeePercentage,
            settlement.FeeAmount,
            settlement.NetAmount,
            settlement.Currency);

        return settlement.ToResponse();
    }

    public async Task<SettlementResponse> GetByIdAsync(
        Guid settlementId,
        CancellationToken cancellationToken = default)
    {
        var settlement = await dbContext.Settlements
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == settlementId,
                cancellationToken);

        if (settlement is null)
        {
            throw new ResourceNotFoundException(
                $"Settlement '{settlementId}' was not found.");
        }

        return settlement.ToResponse();
    }

    public async Task<IReadOnlyList<SettlementResponse>> ListAsync(
        SettlementQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.From.HasValue &&
            query.To.HasValue &&
            query.From.Value > query.To.Value)
        {
            throw new BusinessRuleException(
                "'from' cannot be later than 'to'.");
        }

        var settlements = dbContext.Settlements
            .AsNoTracking()
            .Where(item =>
                item.MerchantId == query.MerchantId);

        if (query.From.HasValue)
        {
            settlements = settlements.Where(
                item =>
                    item.PeriodEnd >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            settlements = settlements.Where(
                item =>
                    item.PeriodStart <= query.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(
                query.Currency))
        {
            var currency = query.Currency
                .Trim()
                .ToUpperInvariant();

            settlements = settlements.Where(
                item => item.Currency == currency);
        }

        var entities = await settlements
            .OrderByDescending(item => item.PeriodEnd)
            .ToListAsync(cancellationToken);

        return entities
            .Select(item => item.ToResponse())
            .ToList();
    }

    private static void ValidateRequest(
        GenerateSettlementRequest request)
    {
        if (request.MerchantId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "MerchantId is required.");
        }

        if (request.PeriodStart >= request.PeriodEnd)
        {
            throw new BusinessRuleException(
                "PeriodStart must be earlier than PeriodEnd.");
        }

        if (request.PeriodEnd >
            DateTimeOffset.UtcNow)
        {
            throw new BusinessRuleException(
                "A settlement period cannot end in the future.");
        }

        var maximumPeriodLength =
            TimeSpan.FromDays(366);

        if (request.PeriodEnd -
            request.PeriodStart >
            maximumPeriodLength)
        {
            throw new BusinessRuleException(
                "A settlement period cannot exceed 366 days.");
        }
    }

    private static string GenerateSettlementReference()
    {
        return $"SET-{DateTimeOffset.UtcNow:yyyyMMdd}-" +
               $"{Guid.NewGuid():N}"
                   .ToUpperInvariant();
    }
}