using System.ComponentModel.DataAnnotations;

namespace SportsbookLite.Api.Features.Betting.Requests;

public sealed record GetBetHistoryRequest
{
    [Required]
    public Guid BetId { get; init; }

    public bool IncludeMetadata { get; init; } = false;
}