namespace SportsbookLite.Contracts.Odds;

[GenerateSerializer]
public readonly record struct OddsUpdateRequest(
    [property: Id(0)] string MarketId,
    [property: Id(1)] Dictionary<string, decimal> SelectionOdds,
    [property: Id(2)] OddsSource Source,
    [property: Id(3)] string? Reason = null,
    [property: Id(4)] string? UpdatedBy = null,
    [property: Id(5)] DateTimeOffset RequestedAt = default)
{
    public static OddsUpdateRequest Create(
        string marketId, 
        Dictionary<string, decimal> selectionOdds, 
        OddsSource source = OddsSource.Manual,
        string? reason = null,
        string? updatedBy = null)
    {
        if (string.IsNullOrWhiteSpace(marketId))
            throw new ArgumentException("MarketId cannot be null or empty", nameof(marketId));
        
        if (selectionOdds == null || !selectionOdds.Any())
            throw new ArgumentException("SelectionOdds cannot be null or empty", nameof(selectionOdds));

        foreach (var (selection, odds) in selectionOdds)
        {
            if (string.IsNullOrWhiteSpace(selection))
                throw new ArgumentException("Selection name cannot be null or empty");
            
            if (odds <= 0)
                throw new ArgumentException($"Odds for selection '{selection}' must be greater than zero");
        }

        return new OddsUpdateRequest(
            MarketId: marketId,
            SelectionOdds: selectionOdds,
            Source: source,
            Reason: reason,
            UpdatedBy: updatedBy,
            RequestedAt: DateTimeOffset.UtcNow);
    }

    public bool IsValidUpdate()
    {
        return !string.IsNullOrWhiteSpace(MarketId) && 
               SelectionOdds.Any() && 
               SelectionOdds.All(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && kvp.Value > 0);
    }
}