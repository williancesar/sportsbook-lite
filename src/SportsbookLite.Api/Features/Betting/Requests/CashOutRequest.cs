using System.ComponentModel.DataAnnotations;

namespace SportsbookLite.Api.Features.Betting.Requests;

public sealed record CashOutRequest
{
    [Required]
    public Guid BetId { get; init; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Acceptable payout must be greater than 0")]
    public decimal? AcceptablePayout { get; init; }
}