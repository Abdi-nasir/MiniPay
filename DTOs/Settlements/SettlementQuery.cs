using System.ComponentModel.DataAnnotations;
namespace MiniApy.Api.DTOs.Settlements;

public sealed class SettlementQuery
{
    [Required]
    public Guid MerchantId { get; set; }

    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }

    [RegularExpression(
        "^[A-Z]{3}$",
        ErrorMessage = "Currency must be a three-letter uppercase code.")]
    public string? Currency { get; set; }
}