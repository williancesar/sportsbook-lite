using SportsbookLite.Contracts.Settlement;
using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.Grains.Settlement;

public enum SagaStep
{
    Started,
    ValidatingRequest,
    CollectingBets,
    CalculatingPayouts,
    ProcessingPayouts,
    UpdatingBets,
    Completed,
    Compensating,
    Failed
}

[GenerateSerializer]
public sealed class SettlementSagaState
{
    [Id(0)] public SettlementStatus Status { get; set; } = SettlementStatus.Pending;
    [Id(1)] public SagaStep CurrentStep { get; set; } = SagaStep.Started;
    [Id(2)] public SettlementRequest? Request { get; set; }
    [Id(3)] public IList<string> ExecutedSteps { get; set; } = new List<string>();
    [Id(4)] public IList<string> CompensationSteps { get; set; } = new List<string>();
    [Id(5)] public IList<Guid> CollectedBetIds { get; set; } = new List<Guid>();
    [Id(6)] public IDictionary<Guid, PayoutCalculation> PayoutCalculations { get; set; } = new Dictionary<Guid, PayoutCalculation>();
    [Id(7)] public IList<Guid> ProcessedPayouts { get; set; } = new List<Guid>();
    [Id(8)] public IList<Guid> UpdatedBets { get; set; } = new List<Guid>();
    [Id(9)] public DateTimeOffset StartedAt { get; set; }
    [Id(10)] public DateTimeOffset? CompletedAt { get; set; }
    [Id(11)] public Money TotalPayouts { get; set; } = Money.Zero();
    [Id(12)] public string? ErrorMessage { get; set; }
    [Id(13)] public int RetryCount { get; set; } = 0;
    [Id(14)] public DateTimeOffset? NextRetryAt { get; set; }
    [Id(15)] public bool IsCompensationRequired { get; set; }
}