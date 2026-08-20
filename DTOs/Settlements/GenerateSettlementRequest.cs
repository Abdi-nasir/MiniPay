using System.ComponentModel.DataAnnotations;

namespace MiniApy.Api.DTOs.Settlements;

public sealed class GenerateSettlementRequest
{
    [Required]
    public Guid MerchantId { get; set; }

    [Required]
    [RegularExpression(
        "^[A-Z]{3}$",
        ErrorMessage =
            "Currency must be a three-letter uppercase code.")]
    public string Currency { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset PeriodStart { get; set; }

    [Required]
    public DateTimeOffset PeriodEnd { get; set; }
}