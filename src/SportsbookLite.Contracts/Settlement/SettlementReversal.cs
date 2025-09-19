using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.Contracts.Settlement;

[GenerateSerializer]
public sealed record SettlementReversal(
    [property: Id(0)] Guid ReversalId,
    [property: Id(1)] Guid OriginalSettlementId,
    [property: Id(2)] string MarketId,
    [property: Id(3)] IReadOnlyList<Guid> AffectedBetIds,
    [property: Id(4)] Money TotalReversalAmount,
    [property: Id(5)] string Reason,
    [property: Id(6)] DateTimeOffset RequestedAt,
    [property: Id(7)] string RequestedBy,
    [property: Id(8)] SettlementStatus Status = SettlementStatus.Pending
)
{
    public static SettlementReversal Create(
        Guid originalSettlementId,
        string marketId,
        IEnumerable<Guid> affectedBetIds,
        Money totalReversalAmount,
        string reason,
        string requestedBy) =>
        new(
            Guid.NewGuid(),
            originalSettlementId,
            marketId,
            affectedBetIds.ToList(),
            totalReversalAmount,
            reason,
            DateTimeOffset.UtcNow,
            requestedBy
        );
}