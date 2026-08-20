namespace MiniApy.Api.RateLimiting;

public static class RateLimitPolicies
{
    public const string MerchantRead = "merchant-read";
    public const string MerchantWrite = "merchant-write";
    public const string Settlement = "settlement";
    public const string Webhook = "webhook";
}