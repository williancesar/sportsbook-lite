using SportsbookLite.Contracts.Betting;

namespace SportsbookLite.Api.Features.Betting.Responses;

public sealed record CashOutResponse
{
    public bool IsSuccess { get; init; }
    public Bet? Bet { get; init; }
    public decimal? PayoutAmount { get; init; }
    public string? Currency { get; init; }
    public decimal? Fees { get; init; }
    public DateTimeOffset? CashedOutAt { get; init; }
    public string? ErrorMessage { get; init; }
}