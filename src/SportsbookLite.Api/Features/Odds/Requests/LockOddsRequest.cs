namespace SportsbookLite.Api.Features.Odds.Requests;

public sealed class LockOddsRequest
{
    public string BetId { get; set; } = string.Empty;
    public string SelectionId { get; set; } = string.Empty;
}