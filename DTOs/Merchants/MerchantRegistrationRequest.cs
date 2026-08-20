using System.ComponentModel.DataAnnotations;
namespace MiniApy.Api.DTOs.Merchants;

public sealed class MerchantRegistrationRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(320)]
    public string Email { get; set; } = string.Empty;

    [Url]
    [StringLength(2_000)]
    public string? WebhookUrl { get; set; }
}