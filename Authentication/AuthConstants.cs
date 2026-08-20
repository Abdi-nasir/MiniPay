namespace MiniApy.Api.Authentication;

public static class AuthConstants
{
    public static class Roles
    {
        public const string Merchant = "minipay-merchant";
        public const string Admin = "minipay-admin";
        public const string Settlement = "minipay-settlement";
    }

    public static class Policies
    {
        public const string Merchant = "Merchant";
        public const string Admin = "Admin";
        public const string Settlement = "Settlement";
        public const string MerchantOrAdmin = "MerchantOrAdmin";
    }

    public static class Claims
    {
        public const string MerchantId = "merchant_id";
    }
}