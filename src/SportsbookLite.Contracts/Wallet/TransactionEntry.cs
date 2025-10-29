namespace SportsbookLite.Contracts.Wallet;

[GenerateSerializer]
public readonly record struct TransactionEntry(
    [property: Id(0)] string Id,
    [property: Id(1)] string TransactionId,
    [property: Id(2)] Money Amount,
    [property: Id(3)] EntryType Type,
    [property: Id(4)] string Description,
    [property: Id(5)] DateTimeOffset Timestamp)
{
    public static TransactionEntry CreateDebit(string transactionId, Money amount, string description)
    {
        return new TransactionEntry(
            Id: Guid.NewGuid().ToString(),
            TransactionId: transactionId,
            Amount: amount,
            Type: EntryType.Debit,
            Description: description,
            Timestamp: DateTimeOffset.UtcNow);
    }
    
    public static TransactionEntry CreateCredit(string transactionId, Money amount, string description)
    {
        return new TransactionEntry(
            Id: Guid.NewGuid().ToString(),
            TransactionId: transactionId,
            Amount: amount,
            Type: EntryType.Credit,
            Description: description,
            Timestamp: DateTimeOffset.UtcNow);
    }
}

[GenerateSerializer]
public enum EntryType
{
    Debit,
    Credit
}