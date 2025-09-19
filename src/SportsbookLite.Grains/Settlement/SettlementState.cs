using SportsbookLite.Contracts.Settlement;
using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.Grains.Settlement;

[GenerateSerializer]
public sealed class SettlementState
{
    [Id(0)] public SettlementStatus Status { get; set; } = SettlementStatus.Pending;
    [Id(1)] public Guid EventId { get; set; }
    [Id(2)] public string MarketId { get; set; } = string.Empty;
    [Id(3)] public string WinningSelectionId { get; set; } = string.Empty;
    [Id(4)] public IList<Guid> AffectedBetIds { get; set; } = new List<Guid>();
    [Id(5)] public IList<Guid> ProcessedBetIds { get; set; } = new List<Guid>();
    [Id(6)] public IList<string> FailedBetIds { get; set; } = new List<string>();
    [Id(7)] public Money TotalPayouts { get; set; } = Money.Zero();
    [Id(8)] public DateTimeOffset StartedAt { get; set; }
    [Id(9)] public DateTimeOffset? CompletedAt { get; set; }
    [Id(10)] public string? CurrentSagaId { get; set; }
    [Id(11)] public int AttemptNumber { get; set; } = 1;
    [Id(12)] public string? LastError { get; set; }
}