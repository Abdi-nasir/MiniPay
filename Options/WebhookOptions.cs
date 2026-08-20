using System.ComponentModel.DataAnnotations;

namespace MiniApy.Api.Options;

public sealed class WebhookOptions
{
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 10;

    [Range(1, 20)]
    public int MaximumAttempts { get; set; } = 5;

    [Range(1, 3_600)]
    public int InitialRetryDelaySeconds { get; set; } = 5;

    [Range(1, 300)]
    public int PollIntervalSeconds { get; set; } = 5;

    [Range(1, 500)]
    public int BatchSize { get; set; } = 25;

    [Required]
    [MinLength(32)]
    public string SigningSecret { get; set; } = string.Empty;
}