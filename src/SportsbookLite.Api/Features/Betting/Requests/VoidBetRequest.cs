using System.ComponentModel.DataAnnotations;

namespace SportsbookLite.Api.Features.Betting.Requests;

public sealed record VoidBetRequest
{
    [Required]
    public Guid BetId { get; init; }

    [Required]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Reason must be between 1 and 500 characters")]
    public string Reason { get; init; } = string.Empty;
}