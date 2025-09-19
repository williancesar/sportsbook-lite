using SportsbookLite.Contracts.Settlement;

namespace SportsbookLite.GrainInterfaces;

public interface IBatchSettlementGrain : IGrainWithIntegerKey
{
    ValueTask<SettlementResult> ProcessBatchAsync(SettlementBatch batch);
    ValueTask<SettlementStatus> GetBatchStatusAsync();
    ValueTask<int> GetProcessedCountAsync();
    ValueTask<int> GetRemainingCountAsync();
    ValueTask<TimeSpan> GetEstimatedTimeRemainingAsync();
    ValueTask CancelBatchAsync();
}