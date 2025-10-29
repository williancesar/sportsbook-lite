namespace SportsbookLite.Api.Features.Odds.Requests;

public sealed class UnlockOddsRequest
{
    public string BetId { get; set; } = string.Empty;
}