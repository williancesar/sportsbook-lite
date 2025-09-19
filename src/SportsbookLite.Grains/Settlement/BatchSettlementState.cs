using SportsbookLite.Contracts.Settlement;

namespace SportsbookLite.Grains.Settlement;

[GenerateSerializer]
public sealed class BatchSettlementState
{
    [Id(0)] public SettlementStatus Status { get; set; } = SettlementStatus.Pending;
    [Id(1)] public SettlementBatch? Batch { get; set; }
    [Id(2)] public int ProcessedCount { get; set; } = 0;
    [Id(3)] public int FailedCount { get; set; } = 0;
    [Id(4)] public IList<string> ProcessedRequests { get; set; } = new List<string>();
    [Id(5)] public IList<string> FailedRequests { get; set; } = new List<string>();
    [Id(6)] public DateTimeOffset StartedAt { get; set; }
    [Id(7)] public DateTimeOffset? CompletedAt { get; set; }
    [Id(8)] public DateTimeOffset? LastActivityAt { get; set; }
    [Id(9)] public bool IsCancelled { get; set; }
}