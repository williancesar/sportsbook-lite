using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public readonly record struct TransactionFailedEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] string UserId,
    [property: Id(4)] Money AttemptedAmount,
    [property: Id(5)] TransactionType TransactionType,
    [property: Id(6)] string TransactionId,
    [property: Id(7)] string ErrorReason,
    [property: Id(8)] string Description) : IDomainEvent
{
    public static TransactionFailedEvent Create(
        string userId,
        Money attemptedAmount,
        TransactionType transactionType,
        string transactionId,
        string errorReason,
        string description)
    {
        return new TransactionFailedEvent(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: userId,
            UserId: userId,
            AttemptedAmount: attemptedAmount,
            TransactionType: transactionType,
            TransactionId: transactionId,
            ErrorReason: errorReason,
            Description: description);
    }
}