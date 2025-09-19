using SportsbookLite.Contracts.Odds;
using OddsValue = SportsbookLite.Contracts.Odds.Odds;

namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public readonly record struct OddsUpdatedEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] string MarketId,
    [property: Id(4)] Dictionary<string, OddsValue> PreviousOdds,
    [property: Id(5)] Dictionary<string, OddsValue> NewOdds,
    [property: Id(6)] OddsSource UpdateSource,
    [property: Id(7)] string? Reason = null,
    [property: Id(8)] string? UpdatedBy = null) : IDomainEvent
{
    public static OddsUpdatedEvent Create(
        string marketId,
        Dictionary<string, OddsValue> previousOdds,
        Dictionary<string, OddsValue> newOdds,
        OddsSource updateSource,
        string? reason = null,
        string? updatedBy = null)
    {
        return new OddsUpdatedEvent(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: marketId,
            MarketId: marketId,
            PreviousOdds: previousOdds,
            NewOdds: newOdds,
            UpdateSource: updateSource,
            Reason: reason,
            UpdatedBy: updatedBy);
    }

    public IEnumerable<string> GetChangedSelections()
    {
        var changedSelections = new HashSet<string>();
        
        foreach (var (selection, newOdd) in NewOdds)
        {
            if (!PreviousOdds.TryGetValue(selection, out var previousOdd) || 
                previousOdd.Decimal != newOdd.Decimal)
            {
                changedSelections.Add(selection);
            }
        }
        
        return changedSelections;
    }

    public decimal GetMaxPercentageChange()
    {
        decimal maxChange = 0;
        
        foreach (var selection in GetChangedSelections())
        {
            if (PreviousOdds.TryGetValue(selection, out var previousOdd) && 
                NewOdds.TryGetValue(selection, out var newOdd))
            {
                var percentageChange = Math.Abs((newOdd.Decimal - previousOdd.Decimal) / previousOdd.Decimal) * 100;
                maxChange = Math.Max(maxChange, percentageChange);
            }
        }
        
        return maxChange;
    }
}