using System.ComponentModel.DataAnnotations;
namespace MiniApy.Api.DTOs.Refunds;


public sealed class CreateRefundRequest
{
    [Required]
    public Guid PaymentId { get; set; }

    [Range(
        typeof(decimal),
        "0.01",
        "9999999999999999.99",
        ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }
}