using SportsbookLite.Contracts.Settlement;

namespace SportsbookLite.GrainInterfaces;

public interface ISettlementSagaGrain : IGrainWithGuidKey
{
    ValueTask<SettlementResult> StartSettlementAsync(SettlementRequest request);
    ValueTask<SettlementResult> CompensateAsync(string reason);
    ValueTask<SettlementStatus> GetSagaStatusAsync();
    ValueTask<bool> IsCompletedAsync();
    ValueTask<bool> IsFailedAsync();
    ValueTask<IReadOnlyList<string>> GetExecutedStepsAsync();
    ValueTask CancelAsync();
}