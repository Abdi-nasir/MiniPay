using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MiniApy.Api.Data;
using MiniApy.Api.DTOs.Merchants;
using MiniApy.Api.Entities;
using MiniApy.Api.Exceptions;
using MiniApy.Api.Interfaces;
using MiniApy.Api.Helpers;

namespace MiniApy.Api.Services;

public sealed class MerchantService(
    AppDbContext dbContext,
    ILogger<MerchantService> logger)
    : IMerchantService
{
    public async Task<MerchantRegistrationResponse> RegisterAsync(
        MerchantRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailAlreadyExists = await dbContext.Merchants
            .AnyAsync(
                merchant => merchant.Email == normalizedEmail,
                cancellationToken);

        if (emailAlreadyExists)
        {
            logger.LogWarning(
        "Attempt to register a merchant with an existing email");
            throw new ResourceConflictException(
                $"A merchant with {normalizedEmail} email address already exists.");
        }

        var apiKey = GenerateApiKey();

        var merchant = new Merchant
        {
            Name = request.Name.Trim(),
            Email = normalizedEmail,
            WebhookUrl = NormalizeOptionalValue(request.WebhookUrl),
            ApiKeyHash = HashApiKey(apiKey),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Merchants.Add(merchant);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Registered merchant {MerchantId} with email {Email}",
            merchant.Id,
            merchant.Email);

        return new MerchantRegistrationResponse(
            merchant.Id,
            merchant.Name,
            merchant.Email,
            merchant.WebhookUrl,
            merchant.IsActive,
            apiKey,
            merchant.CreatedAt);
    }

    public async Task<IReadOnlyList<MerchantResponse>> ListAsync(
        MerchantsQuery query,
        CancellationToken cancellationToken = default)
    {
        
        var merchants =  dbContext.Merchants
            .AsNoTracking()
            .AsQueryable();

            var skip = (query.Page - 1) * query.PageSize;
            

        return await merchants
            .OrderByDescending(merchant => merchant.CreatedAt)
            .Skip(skip)
            .Take(query.PageSize)
            .Select(merchant => merchant.ToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task<MerchantResponse> GetByIdAsync(
        Guid merchantId,
        CancellationToken cancellationToken = default)
    {
        var merchant = await dbContext.Merchants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == merchantId,
                cancellationToken);

        if (merchant is null)
        {
            throw new ResourceNotFoundException(
                $"Merchant '{merchantId}' was not found.");
        }

        return merchant.ToResponse();
    }

    private static string GenerateApiKey()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var secret = Convert.ToHexString(randomBytes).ToLowerInvariant();

        return $"mp_test_{secret}";
    }

    private static string HashApiKey(string apiKey)
    {
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}