using System.Security.Cryptography;
using System.Text;

namespace MiniApy.Api.Helpers;

public static class WebhookSignature
{
    public static string Create(
        string signingSecret,
        string timestamp,
        string payload)
    {
        var signedContent = $"{timestamp}.{payload}";

        var keyBytes = Encoding.UTF8.GetBytes(signingSecret);
        var contentBytes = Encoding.UTF8.GetBytes(signedContent);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(contentBytes);

        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}