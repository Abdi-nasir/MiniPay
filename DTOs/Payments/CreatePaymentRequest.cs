using System.ComponentModel.DataAnnotations;
namespace MiniApy.Api.DTOs.Payments;

public sealed class CreatePaymentRequest
{
    [Required]
    public Guid MerchantId { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Reference { get; set; } = string.Empty;

    [Range(
        typeof(decimal),
        "0.01",
        "9999999999999999.99",
        ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Required]
    [RegularExpression(
        "^[A-Z]{3}$",
        ErrorMessage = "Currency must be a three-letter uppercase code.")]
    public string Currency { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }
}