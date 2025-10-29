namespace SportsbookLite.Contracts.Odds;

[GenerateSerializer]
public readonly record struct OddsHistory(
    [property: Id(0)] string MarketId,
    [property: Id(1)] string Selection,
    [property: Id(2)] IReadOnlyList<OddsUpdate> Updates,
    [property: Id(3)] DateTimeOffset CreatedAt,
    [property: Id(4)] DateTimeOffset LastModified)
{
    public static OddsHistory Create(string marketId, string selection, Odds initialOdds)
    {
        var now = DateTimeOffset.UtcNow;
        var updates = new List<OddsUpdate>();
        
        return new OddsHistory(
            MarketId: marketId,
            Selection: selection,
            Updates: updates,
            CreatedAt: now,
            LastModified: now);
    }

    public OddsHistory AddUpdate(OddsUpdate update)
    {
        var updatedList = Updates.ToList();
        updatedList.Add(update);
        
        return this with 
        { 
            Updates = updatedList,
            LastModified = DateTimeOffset.UtcNow 
        };
    }

    public Odds? GetCurrentOdds()
    {
        return Updates.LastOrDefault().NewOdds;
    }

    public IEnumerable<OddsUpdate> GetUpdatesInTimeWindow(TimeSpan window)
    {
        var cutoffTime = DateTimeOffset.UtcNow.Subtract(window);
        return Updates.Where(u => u.UpdatedAt >= cutoffTime);
    }

    public decimal CalculateVolatilityScore(TimeSpan window)
    {
        var recentUpdates = GetUpdatesInTimeWindow(window).ToList();
        
        if (recentUpdates.Count < 2)
            return 0;

        var totalPercentageChange = recentUpdates.Sum(u => u.PercentageChange);
        var updateFrequency = (decimal)(recentUpdates.Count / window.TotalHours);
        
        return totalPercentageChange * updateFrequency;
    }

    public OddsVolatility GetVolatilityLevel(TimeSpan window)
    {
        var score = CalculateVolatilityScore(window);
        
        return score switch
        {
            < 10 => OddsVolatility.Low,
            < 25 => OddsVolatility.Medium,
            < 50 => OddsVolatility.High,
            _ => OddsVolatility.Extreme
        };
    }
}