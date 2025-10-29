using SportsbookLite.Contracts.Odds;

namespace SportsbookLite.Api.Features.Odds.Responses;

public sealed class VolatilityResponse
{
    public string MarketId { get; set; } = string.Empty;
    public OddsVolatility Level { get; set; }
    public decimal Score { get; set; }
    public Dictionary<string, VolatilityMetrics> SelectionMetrics { get; set; } = new();
    public DateTimeOffset CalculatedAt { get; set; }
    public TimeSpan Window { get; set; }
}

public sealed class VolatilityMetrics
{
    public string Selection { get; set; } = string.Empty;
    public decimal VolatilityScore { get; set; }
    public OddsVolatility Level { get; set; }
    public int UpdateCount { get; set; }
    public decimal MaxPercentageChange { get; set; }
    public decimal AveragePercentageChange { get; set; }
}