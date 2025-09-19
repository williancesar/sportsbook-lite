namespace SportsbookLite.Contracts.Odds;

[GenerateSerializer]
public readonly record struct OddsSnapshot(
    [property: Id(0)] string MarketId,
    [property: Id(1)] Dictionary<string, Odds> Selections,
    [property: Id(2)] DateTimeOffset SnapshotTime,
    [property: Id(3)] OddsVolatility Volatility,
    [property: Id(4)] bool IsSuspended = false,
    [property: Id(5)] string? SuspensionReason = null)
{
    public static OddsSnapshot Create(string marketId, Dictionary<string, Odds> selections, OddsVolatility volatility = OddsVolatility.Low)
    {
        return new OddsSnapshot(
            MarketId: marketId,
            Selections: selections,
            SnapshotTime: DateTimeOffset.UtcNow,
            Volatility: volatility);
    }

    public OddsSnapshot WithSuspension(string reason)
    {
        return this with 
        { 
            IsSuspended = true,
            SuspensionReason = reason,
            SnapshotTime = DateTimeOffset.UtcNow 
        };
    }

    public OddsSnapshot WithResumption()
    {
        return this with 
        { 
            IsSuspended = false,
            SuspensionReason = null,
            SnapshotTime = DateTimeOffset.UtcNow 
        };
    }

    public OddsSnapshot WithUpdatedSelections(Dictionary<string, Odds> selections, OddsVolatility volatility)
    {
        return this with 
        { 
            Selections = selections,
            Volatility = volatility,
            SnapshotTime = DateTimeOffset.UtcNow 
        };
    }

    public Odds? GetOddsForSelection(string selection)
    {
        return Selections.TryGetValue(selection, out var odds) ? odds : null;
    }

    public decimal GetTotalMargin()
    {
        if (!Selections.Any())
            return 0;

        var totalImpliedProbability = Selections.Values.Sum(o => o.ImpliedProbability);
        return (decimal)(totalImpliedProbability - 1) * 100;
    }
}