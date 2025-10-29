using SportsbookLite.Contracts.Odds;
using OddsValue = SportsbookLite.Contracts.Odds.Odds;

namespace SportsbookLite.Grains.Odds;

[GenerateSerializer]
public sealed class OddsState
{
    [Id(0)]
    public string MarketId { get; set; } = string.Empty;

    [Id(1)]
    public Dictionary<string, OddsValue> CurrentOdds { get; set; } = new();

    [Id(2)]
    public Dictionary<string, OddsHistory> OddsHistories { get; set; } = new();

    [Id(3)]
    public bool IsSuspended { get; set; } = false;

    [Id(4)]
    public string? SuspensionReason { get; set; }

    [Id(5)]
    public DateTimeOffset? SuspensionTime { get; set; }

    [Id(6)]
    public OddsVolatility CurrentVolatility { get; set; } = OddsVolatility.Low;

    [Id(7)]
    public Dictionary<string, HashSet<string>> LockedSelections { get; set; } = new();

    [Id(8)]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Id(9)]
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

    [Id(10)]
    public decimal VolatilityThreshold { get; set; } = 50.0m;

    [Id(11)]
    public TimeSpan VolatilityWindow { get; set; } = TimeSpan.FromHours(1);

    public void Initialize(string marketId, Dictionary<string, decimal> initialOdds, OddsSource source)
    {
        MarketId = marketId;
        CreatedAt = DateTimeOffset.UtcNow;
        LastModified = DateTimeOffset.UtcNow;
        
        foreach (var (selection, odds) in initialOdds)
        {
            var oddsValue = OddsValue.Create(odds, marketId, selection, source);
            CurrentOdds[selection] = oddsValue;
            OddsHistories[selection] = OddsHistory.Create(marketId, selection, oddsValue);
        }
    }

    public void UpdateOdds(OddsUpdateRequest request)
    {
        var previousOdds = new Dictionary<string, OddsValue>(CurrentOdds);
        
        foreach (var (selection, newOdds) in request.SelectionOdds)
        {
            if (CurrentOdds.TryGetValue(selection, out var current))
            {
                var newOddsValue = OddsValue.Create(newOdds, MarketId, selection, request.Source);
                var update = OddsUpdate.Create(current, newOddsValue, request.Source, request.Reason);
                
                CurrentOdds[selection] = newOddsValue;
                
                if (OddsHistories.TryGetValue(selection, out var history))
                {
                    OddsHistories[selection] = history.AddUpdate(update);
                }
                else
                {
                    OddsHistories[selection] = OddsHistory.Create(MarketId, selection, newOddsValue);
                }
            }
            else
            {
                var newOddsValue = OddsValue.Create(newOdds, MarketId, selection, request.Source);
                CurrentOdds[selection] = newOddsValue;
                OddsHistories[selection] = OddsHistory.Create(MarketId, selection, newOddsValue);
            }
        }
        
        LastModified = DateTimeOffset.UtcNow;
    }

    public OddsVolatility CalculateVolatility(TimeSpan window)
    {
        if (!OddsHistories.Any())
            return OddsVolatility.Low;

        var maxScore = 0m;
        foreach (var history in OddsHistories.Values)
        {
            var score = history.CalculateVolatilityScore(window);
            maxScore = Math.Max(maxScore, score);
        }

        CurrentVolatility = maxScore switch
        {
            < 10 => OddsVolatility.Low,
            < 25 => OddsVolatility.Medium,
            < 50 => OddsVolatility.High,
            _ => OddsVolatility.Extreme
        };

        return CurrentVolatility;
    }

    public decimal GetVolatilityScore(TimeSpan window)
    {
        if (!OddsHistories.Any())
            return 0;

        return OddsHistories.Values.Max(h => h.CalculateVolatilityScore(window));
    }

    public void Suspend(string reason, string? suspendedBy = null)
    {
        IsSuspended = true;
        SuspensionReason = reason;
        SuspensionTime = DateTimeOffset.UtcNow;
        LastModified = DateTimeOffset.UtcNow;
    }

    public void Resume(string reason)
    {
        IsSuspended = false;
        SuspensionReason = null;
        SuspensionTime = null;
        LastModified = DateTimeOffset.UtcNow;
    }

    public void LockSelection(string selection, string betId)
    {
        if (!LockedSelections.TryGetValue(selection, out var locks))
        {
            locks = new HashSet<string>();
            LockedSelections[selection] = locks;
        }
        
        locks.Add(betId);
        LastModified = DateTimeOffset.UtcNow;
    }

    public void UnlockSelection(string betId)
    {
        foreach (var (selection, locks) in LockedSelections.ToList())
        {
            if (locks.Remove(betId))
            {
                if (!locks.Any())
                {
                    LockedSelections.Remove(selection);
                }
                break;
            }
        }
        LastModified = DateTimeOffset.UtcNow;
    }

    public bool IsSelectionLocked(string selection)
    {
        return LockedSelections.TryGetValue(selection, out var locks) && locks.Any();
    }

    public OddsSnapshot CreateSnapshot()
    {
        return new OddsSnapshot(
            MarketId: MarketId,
            Selections: CurrentOdds,
            SnapshotTime: DateTimeOffset.UtcNow,
            Volatility: CurrentVolatility,
            IsSuspended: IsSuspended,
            SuspensionReason: SuspensionReason);
    }
}