using System.ComponentModel.DataAnnotations;

namespace SportsbookLite.Api.Features.Betting.Requests;

public sealed record PlaceBetApiRequest
{
    [Required]
    public string UserId { get; init; } = string.Empty;

    [Required]
    public Guid EventId { get; init; }

    [Required]
    public string MarketId { get; init; } = string.Empty;

    [Required]
    public string SelectionId { get; init; } = string.Empty;

    [Required]
    [Range(0.01, 10000.00, ErrorMessage = "Stake must be between 0.01 and 10,000")]
    public decimal Stake { get; init; }

    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be exactly 3 characters")]
    public string Currency { get; init; } = "USD";

    [Required]
    [Range(1.01, 1000.00, ErrorMessage = "Acceptable odds must be between 1.01 and 1,000")]
    public decimal AcceptableOdds { get; init; }

    public string? IdempotencyKey { get; init; }
}