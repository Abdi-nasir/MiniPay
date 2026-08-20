using System.ComponentModel.DataAnnotations;

namespace MiniApy.Api.Options;

public sealed class SettlementOptions
{
    [Range(
        typeof(decimal),
        "0",
        "100",
        ErrorMessage =
            "Fee percentage must be between 0 and 100.")]
    public decimal FeePercentage { get; set; } = 2.0m;

    [Range(0, 60_000)]
    public int ProcessingDelayMilliseconds { get; set; } = 500;
}