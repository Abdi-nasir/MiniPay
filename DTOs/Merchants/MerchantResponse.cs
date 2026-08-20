namespace MiniApy.Api.DTOs.Merchants;


public sealed record MerchantResponse(
    Guid Id,
    string Name,
    string Email,
    string? WebhookUrl,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record MerchantRegistrationResponse(
    Guid Id,
    string Name,
    string Email,
    string? WebhookUrl,
    bool IsActive,
    string ApiKey,
    DateTimeOffset CreatedAt);