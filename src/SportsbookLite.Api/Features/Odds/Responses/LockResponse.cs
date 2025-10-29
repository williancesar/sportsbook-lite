namespace SportsbookLite.Api.Features.Odds.Responses;

public sealed class LockResponse
{
    public string MarketId { get; set; } = string.Empty;
    public string BetId { get; set; } = string.Empty;
    public string SelectionId { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public DateTimeOffset LockTimestamp { get; set; }
    public OddsDto? LockedOdds { get; set; }
    public string Message { get; set; } = string.Empty;
}