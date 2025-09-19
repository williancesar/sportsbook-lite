using Bogus;
using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.TestUtilities.TestDataBuilders;

public class WalletTestDataBuilder
{
    private static readonly Faker _faker = new Faker();

    public static WalletTransaction CreateValidDepositTransaction(
        string? userId = null,
        decimal? amount = null,
        string? currency = null,
        string? transactionId = null,
        string? description = null)
    {
        return WalletTransaction.Create(
            userId ?? Guid.NewGuid().ToString(),
            TransactionType.Deposit,
            Money.Create(amount ?? 100m, currency ?? "USD"),
            description ?? "Test deposit transaction",
            transactionId ?? Guid.NewGuid().ToString())
            .WithStatus(TransactionStatus.Completed);
    }

    public static WalletTransaction CreateValidWithdrawalTransaction(
        string? userId = null,
        decimal? amount = null,
        string? currency = null,
        string? transactionId = null,
        string? description = null)
    {
        return WalletTransaction.Create(
            userId ?? Guid.NewGuid().ToString(),
            TransactionType.Withdrawal,
            Money.Create(amount ?? 50m, currency ?? "USD"),
            description ?? "Test withdrawal transaction",
            transactionId ?? Guid.NewGuid().ToString())
            .WithStatus(TransactionStatus.Completed);
    }

    public static WalletTransaction CreateValidReservationTransaction(
        string? userId = null,
        string? betId = null,
        decimal? amount = null,
        string? currency = null)
    {
        return WalletTransaction.Create(
            userId ?? Guid.NewGuid().ToString(),
            TransactionType.Reservation,
            Money.Create(amount ?? 25m, currency ?? "USD"),
            $"Reserve for bet {betId ?? Guid.NewGuid().ToString()}",
            betId ?? Guid.NewGuid().ToString())
            .WithStatus(TransactionStatus.Completed);
    }

    public static WalletTransaction CreateFailedTransaction(
        string? userId = null,
        TransactionType? transactionType = null,
        string? errorMessage = null)
    {
        return WalletTransaction.Create(
            userId ?? Guid.NewGuid().ToString(),
            transactionType ?? TransactionType.Withdrawal,
            Money.Create(100m),
            "Failed transaction test",
            Guid.NewGuid().ToString())
            .WithStatus(TransactionStatus.Failed, errorMessage ?? "Insufficient funds");
    }

    public static Money CreateValidMoney(
        decimal? amount = null,
        string? currency = null)
    {
        return Money.Create(amount ?? 100m, currency ?? "USD");
    }

    public static Money CreateRandomMoney(
        decimal minAmount = 0.01m,
        decimal maxAmount = 10000m,
        string? currency = null)
    {
        return Money.Create(
            _faker.Random.Decimal(minAmount, maxAmount),
            currency ?? _faker.PickRandom("USD", "EUR", "GBP"));
    }

    public static TransactionEntry CreateValidCreditEntry(
        string? transactionId = null,
        decimal? amount = null,
        string? description = null)
    {
        return TransactionEntry.CreateCredit(
            transactionId ?? Guid.NewGuid().ToString(),
            Money.Create(amount ?? 100m),
            description ?? "Test credit entry");
    }

    public static TransactionEntry CreateValidDebitEntry(
        string? transactionId = null,
        decimal? amount = null,
        string? description = null)
    {
        return TransactionEntry.CreateDebit(
            transactionId ?? Guid.NewGuid().ToString(),
            Money.Create(amount ?? 100m),
            description ?? "Test debit entry");
    }

    public static List<WalletTransaction> CreateTransactionHistory(
        string userId,
        int count = 10,
        bool includeFailures = true)
    {
        var transactions = new List<WalletTransaction>();

        for (int i = 0; i < count; i++)
        {
            var transactionType = _faker.PickRandom<TransactionType>();
            var amount = Money.Create(_faker.Random.Decimal(1m, 1000m));
            var status = includeFailures && _faker.Random.Bool(0.1f) 
                ? TransactionStatus.Failed 
                : TransactionStatus.Completed;

            var transaction = WalletTransaction.Create(
                userId,
                transactionType,
                amount,
                $"Test {transactionType.ToString().ToLower()} #{i + 1}",
                Guid.NewGuid().ToString());

            transactions.Add(transaction.WithStatus(status, 
                status == TransactionStatus.Failed ? "Test error" : null));
        }

        return transactions.OrderByDescending(t => t.Timestamp).ToList();
    }

    public static List<TransactionEntry> CreateLedgerEntries(
        int transactionCount = 5)
    {
        var entries = new List<TransactionEntry>();

        for (int i = 0; i < transactionCount; i++)
        {
            var transactionId = Guid.NewGuid().ToString();
            var amount = Money.Create(_faker.Random.Decimal(1m, 500m));

            entries.Add(TransactionEntry.CreateDebit(transactionId, amount, $"Test debit {i + 1}"));
            entries.Add(TransactionEntry.CreateCredit(transactionId, amount, $"Test credit {i + 1}"));
        }

        return entries.OrderByDescending(e => e.Timestamp).ToList();
    }

    public static TransactionResult CreateSuccessfulResult(
        WalletTransaction? transaction = null,
        Money? newBalance = null)
    {
        return TransactionResult.Success(
            transaction ?? CreateValidDepositTransaction(),
            newBalance ?? Money.Create(1000m));
    }

    public static TransactionResult CreateFailedResult(
        string? errorMessage = null)
    {
        return TransactionResult.Failure(
            errorMessage ?? "Test error message");
    }

    public static WalletTransaction CreateRandomTransaction()
    {
        var transactionType = _faker.PickRandom<TransactionType>();
        var amount = Money.Create(_faker.Random.Decimal(0.01m, 10000m));
        var status = _faker.PickRandom<TransactionStatus>();
        
        var transaction = WalletTransaction.Create(
            _faker.Random.Guid().ToString(),
            transactionType,
            amount,
            _faker.Lorem.Sentence(3, 5),
            _faker.Random.Guid().ToString());
            
        return transaction.WithStatus(status, 
            status == TransactionStatus.Failed ? _faker.Lorem.Sentence(2, 4) : null);
    }

    public static List<WalletTransaction> CreateRandomTransactions(int count)
    {
        var transactions = new List<WalletTransaction>();
        for (int i = 0; i < count; i++)
        {
            transactions.Add(CreateRandomTransaction());
        }
        return transactions.OrderByDescending(t => t.Timestamp).ToList();
    }

    public static Money CreateZeroMoney(string currency = "USD")
    {
        return Money.Zero(currency);
    }

    public static List<TransactionEntry> CreateBalancedLedgerEntries(
        int pairCount = 5,
        string? currency = null)
    {
        var entries = new List<TransactionEntry>();
        currency ??= "USD";

        for (int i = 0; i < pairCount; i++)
        {
            var transactionId = Guid.NewGuid().ToString();
            var amount = Money.Create(_faker.Random.Decimal(1m, 100m), currency);

            entries.Add(TransactionEntry.CreateDebit(
                transactionId, 
                amount, 
                $"Balanced debit entry {i + 1}"));
                
            entries.Add(TransactionEntry.CreateCredit(
                transactionId, 
                amount, 
                $"Balanced credit entry {i + 1}"));
        }

        return entries.OrderByDescending(e => e.Timestamp).ToList();
    }

    public static class CommonAmounts
    {
        public static Money OneHundredUsd => Money.Create(100m, "USD");
        public static Money FiftyUsd => Money.Create(50m, "USD");
        public static Money TwentyFiveUsd => Money.Create(25m, "USD");
        public static Money OneThousandUsd => Money.Create(1000m, "USD");
        public static Money OneCent => Money.Create(0.01m, "USD");
        public static Money OneHundredEur => Money.Create(100m, "EUR");
        public static Money FiftyGbp => Money.Create(50m, "GBP");
    }

    public static class CommonUserIds
    {
        public static string TestUser1 => "test-user-001";
        public static string TestUser2 => "test-user-002";
        public static string TestUser3 => "test-user-003";
        public static string IntegrationTestUser => "integration-test-user";
        public static string PerformanceTestUser => "performance-test-user";
    }

    public static class CommonTransactionIds
    {
        public static string DepositTxn => $"deposit-{Guid.NewGuid()}";
        public static string WithdrawTxn => $"withdraw-{Guid.NewGuid()}";
        public static string ReserveTxn => $"reserve-{Guid.NewGuid()}";
        public static string CommitTxn => $"commit-{Guid.NewGuid()}";
        public static string ReleaseTxn => $"release-{Guid.NewGuid()}";
    }
}