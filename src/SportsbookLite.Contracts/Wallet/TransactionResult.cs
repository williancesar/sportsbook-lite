namespace SportsbookLite.Contracts.Wallet;

[GenerateSerializer]
public readonly record struct TransactionResult(
    [property: Id(0)] bool IsSuccess,
    [property: Id(1)] string? ErrorMessage,
    [property: Id(2)] WalletTransaction? Transaction = null,
    [property: Id(3)] Money? NewBalance = null)
{
    public static TransactionResult Success(WalletTransaction transaction, Money newBalance)
    {
        return new TransactionResult(true, null, transaction, newBalance);
    }
    
    public static TransactionResult Failure(string errorMessage)
    {
        return new TransactionResult(false, errorMessage);
    }
}