using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.Grains.Wallet;

[GenerateSerializer]
public sealed class WalletState
{
    [Id(0)]
    public string UserId { get; set; } = string.Empty;

    [Id(1)]
    public string Currency { get; set; } = "USD";

    [Id(2)]
    public decimal Balance { get; set; } = 0m;

    [Id(3)]
    public decimal ReservedAmount { get; set; } = 0m;

    [Id(4)]
    public List<WalletTransaction> Transactions { get; set; } = new();

    [Id(5)]
    public List<TransactionEntry> LedgerEntries { get; set; } = new();

    [Id(6)]
    public Dictionary<string, decimal> Reservations { get; set; } = new();

    [Id(7)]
    public HashSet<string> ProcessedTransactionIds { get; set; } = new();

    [Id(8)]
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

    public Money GetBalance() => new(Balance, Currency);
    public Money GetAvailableBalance() => new(Balance - ReservedAmount, Currency);
    public Money GetReservedAmount() => new(ReservedAmount, Currency);

    public bool HasSufficientBalance(Money amount)
    {
        if (Currency != amount.Currency) return false;
        return (Balance - ReservedAmount) >= amount.Amount;
    }

    public void AddTransaction(WalletTransaction transaction)
    {
        Transactions.Add(transaction);
        if (!string.IsNullOrEmpty(transaction.ReferenceId))
        {
            ProcessedTransactionIds.Add(transaction.ReferenceId);
        }
        else
        {
            ProcessedTransactionIds.Add(transaction.Id);
        }
        LastModified = DateTimeOffset.UtcNow;
    }

    public void AddLedgerEntry(TransactionEntry entry)
    {
        LedgerEntries.Add(entry);
        LastModified = DateTimeOffset.UtcNow;
    }

    public void UpdateBalance(decimal newBalance)
    {
        Balance = newBalance;
        LastModified = DateTimeOffset.UtcNow;
    }

    public void AddReservation(string betId, decimal amount)
    {
        Reservations[betId] = amount;
        ReservedAmount += amount;
        LastModified = DateTimeOffset.UtcNow;
    }

    public void RemoveReservation(string betId)
    {
        if (Reservations.TryGetValue(betId, out var amount))
        {
            Reservations.Remove(betId);
            ReservedAmount -= amount;
            LastModified = DateTimeOffset.UtcNow;
        }
    }

    public bool HasReservation(string betId) => Reservations.ContainsKey(betId);

    public decimal GetReservationAmount(string betId) =>
        Reservations.TryGetValue(betId, out var amount) ? amount : 0m;
}