using SportsbookLite.Contracts.Odds;

namespace SportsbookLite.Api.Features.Odds.Responses;

public sealed class OddsResponse
{
    public string MarketId { get; set; } = string.Empty;
    public Dictionary<string, OddsDto> Selections { get; set; } = new();
    public DateTimeOffset SnapshotTime { get; set; }
    public OddsVolatility Volatility { get; set; }
    public bool IsSuspended { get; set; }
    public string? SuspensionReason { get; set; }
    public decimal TotalMargin { get; set; }
}

public sealed class OddsDto
{
    public decimal Decimal { get; set; }
    public decimal Fractional { get; set; }
    public int American { get; set; }
    public decimal ImpliedProbability { get; set; }
    public string MarketId { get; set; } = string.Empty;
    public string Selection { get; set; } = string.Empty;
    public OddsSource Source { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}