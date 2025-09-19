namespace SportsbookLite.Contracts.Wallet;

[GenerateSerializer]
public readonly record struct WalletTransaction(
    [property: Id(0)] string Id,
    [property: Id(1)] string UserId,
    [property: Id(2)] TransactionType Type,
    [property: Id(3)] Money Amount,
    [property: Id(4)] TransactionStatus Status,
    [property: Id(5)] string Description,
    [property: Id(6)] DateTimeOffset Timestamp,
    [property: Id(7)] string? ReferenceId = null,
    [property: Id(8)] string? ErrorMessage = null)
{
    public static WalletTransaction Create(
        string userId,
        TransactionType type,
        Money amount,
        string description,
        string? referenceId = null)
    {
        return new WalletTransaction(
            Id: Guid.NewGuid().ToString(),
            UserId: userId,
            Type: type,
            Amount: amount,
            Status: TransactionStatus.Pending,
            Description: description,
            Timestamp: DateTimeOffset.UtcNow,
            ReferenceId: referenceId);
    }
    
    public WalletTransaction WithStatus(TransactionStatus status, string? errorMessage = null)
    {
        return this with { Status = status, ErrorMessage = errorMessage };
    }
}