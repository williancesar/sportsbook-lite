using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public readonly record struct WalletDebitedEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] string UserId,
    [property: Id(4)] Money Amount,
    [property: Id(5)] TransactionType TransactionType,
    [property: Id(6)] string TransactionId,
    [property: Id(7)] string Description,
    [property: Id(8)] Money NewBalance) : IDomainEvent
{
    public static WalletDebitedEvent Create(
        string userId,
        Money amount,
        TransactionType transactionType,
        string transactionId,
        string description,
        Money newBalance)
    {
        return new WalletDebitedEvent(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: userId,
            UserId: userId,
            Amount: amount,
            TransactionType: transactionType,
            TransactionId: transactionId,
            Description: description,
            NewBalance: newBalance);
    }
}