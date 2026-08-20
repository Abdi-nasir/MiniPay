using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniApy.Api.DTOs.Payments;
using MiniApy.Api.Exceptions;

namespace MiniApy.Api.Helpers;

public static class IdempotencyHelper
{
    public static string ValidateKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new BusinessRuleException(
                "The Idempotency-Key header is required.");
        }

        var normalizedKey = key.Trim();

        if (normalizedKey.Length > 200)
        {
            throw new BusinessRuleException(
                "The Idempotency-Key header cannot exceed 200 characters.");
        }

        return normalizedKey;
    }

    public static string HashPaymentRequest(
        Guid merchantId,
        CreatePaymentRequest request)
    {
        var normalizedRequest = new
        {
            MerchantId = merchantId,
            Reference = request.Reference.Trim(),
            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Description = string.IsNullOrWhiteSpace(
                request.Description)
                    ? null
                    : request.Description.Trim()
        };

        var json = JsonSerializer.Serialize(normalizedRequest);

        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(json));

        return Convert.ToHexString(bytes);
    }
}