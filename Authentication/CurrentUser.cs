using System.Security.Claims;

namespace MiniApy.Api.Authentication;

public sealed class CurrentUser(IHttpContextAccessor accessor)
{
    public bool IsAdmin =>
        accessor.HttpContext?.User.IsInRole(
            AuthConstants.Roles.Admin) == true;

    public Guid GetMerchantId()
    {
        var value = accessor.HttpContext?.User
            .FindFirstValue(AuthConstants.Claims.MerchantId);

        if (!Guid.TryParse(value, out var merchantId))
        {
            throw new UnauthorizedAccessException(
                "The token does not contain a valid merchant_id claim.");
        }

        return merchantId;
    }
}