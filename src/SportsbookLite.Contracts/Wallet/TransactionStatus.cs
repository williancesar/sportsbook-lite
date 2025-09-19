namespace SportsbookLite.Contracts.Wallet;

[GenerateSerializer]
public enum TransactionStatus
{
    Pending,
    Completed,
    Failed,
    Cancelled
}