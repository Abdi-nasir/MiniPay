namespace MiniApy.Api.Caching;

public static class CacheKeys
{
    public static string Merchant(Guid merchantId) =>
        $"merchant:{merchantId}";

    public static string Payment(
        Guid merchantId,
        Guid paymentId) =>
        $"merchant:{merchantId}:payment:{paymentId}";

    public static string PaymentList(
        Guid merchantId,
        int page,
        int pageSize) =>
        $"merchant:{merchantId}:payments:{page}:{pageSize}";

    public static string MerchantPaymentsTag(
        Guid merchantId) =>
        $"merchant:{merchantId}:payments";
}