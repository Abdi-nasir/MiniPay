using System.ComponentModel.DataAnnotations;
namespace MiniApy.Api.DTOs.Payments;

public sealed class ConfirmPaymentRequest
{
    public bool SimulateFailure { get; set; }

    [StringLength(500)]
    public string? FailureReason { get; set; }
}