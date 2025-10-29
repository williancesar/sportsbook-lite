using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.Contracts.Settlement;

[GenerateSerializer]
public sealed record SettlementResult(
    [property: Id(0)] bool IsSuccess,
    [property: Id(1)] SettlementStatus Status,
    [property: Id(2)] IReadOnlyList<Guid> AffectedBetIds,
    [property: Id(3)] Money TotalPayouts,
    [property: Id(4)] int SuccessfulSettlements,
    [property: Id(5)] int FailedSettlements,
    [property: Id(6)] string? ErrorMessage = null,
    [property: Id(7)] IReadOnlyList<string>? Errors = null
)
{
    public static SettlementResult Success(
        IReadOnlyList<Guid> affectedBetIds, 
        Money totalPayouts,
        int successfulSettlements) =>
        new(true, SettlementStatus.Completed, affectedBetIds, totalPayouts, successfulSettlements, 0);

    public static SettlementResult Failed(string errorMessage) =>
        new(false, SettlementStatus.Failed, Array.Empty<Guid>(), Money.Zero(), 0, 0, errorMessage);

    public static SettlementResult Partial(
        IReadOnlyList<Guid> affectedBetIds,
        Money totalPayouts,
        int successfulSettlements,
        int failedSettlements,
        IReadOnlyList<string> errors) =>
        new(true, SettlementStatus.PartiallyCompleted, affectedBetIds, totalPayouts, successfulSettlements, failedSettlements, null, errors);
}