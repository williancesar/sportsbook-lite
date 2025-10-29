namespace SportsbookLite.Contracts.Settlement;

[GenerateSerializer]
public sealed record SettlementRequest(
    [property: Id(0)] Guid EventId,
    [property: Id(1)] string MarketId,
    [property: Id(2)] string WinningSelectionId,
    [property: Id(3)] DateTimeOffset RequestedAt,
    [property: Id(4)] string RequestedBy,
    [property: Id(5)] string? Reason = null
);