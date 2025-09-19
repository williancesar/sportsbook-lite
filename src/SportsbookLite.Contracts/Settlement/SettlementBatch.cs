namespace SportsbookLite.Contracts.Settlement;

[GenerateSerializer]
public sealed record SettlementBatch(
    [property: Id(0)] Guid BatchId,
    [property: Id(1)] IReadOnlyList<SettlementRequest> Requests,
    [property: Id(2)] DateTimeOffset CreatedAt,
    [property: Id(3)] int Priority = 0,
    [property: Id(4)] TimeSpan? ProcessingTimeout = null
)
{
    public static SettlementBatch Create(IEnumerable<SettlementRequest> requests, int priority = 0, TimeSpan? timeout = null) =>
        new(Guid.NewGuid(), requests.ToList(), DateTimeOffset.UtcNow, priority, timeout);

    public int Count => Requests.Count;
    
    public IEnumerable<Guid> EventIds => Requests.Select(r => r.EventId).Distinct();
    
    public IEnumerable<string> MarketIds => Requests.Select(r => r.MarketId).Distinct();
}