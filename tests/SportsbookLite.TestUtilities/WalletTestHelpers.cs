using FluentAssertions;
using Orleans;
using SportsbookLite.Contracts.Wallet;
using SportsbookLite.GrainInterfaces;
using SportsbookLite.TestUtilities.TestDataBuilders;

namespace SportsbookLite.TestUtilities;

public static class WalletTestHelpers
{
    public static async Task<IUserWalletGrain> SetupWalletWithBalanceAsync(
        IGrainFactory grainFactory,
        string userId,
        decimal initialBalance,
        string currency = "USD")
    {
        var grain = grainFactory.GetGrain<IUserWalletGrain>(userId);
        var transactionId = Guid.NewGuid().ToString();
        var amount = Money.Create(initialBalance, currency);

        var result = await grain.DepositAsync(amount, transactionId);
        result.IsSuccess.Should().BeTrue("Initial balance setup should succeed");

        return grain;
    }

    public static async Task<IUserWalletGrain> SetupWalletWithTransactionHistoryAsync(
        IGrainFactory grainFactory,
        string userId,
        int numberOfTransactions = 10,
        decimal baseAmount = 100m,
        string currency = "USD")
    {
        var grain = grainFactory.GetGrain<IUserWalletGrain>(userId);
        var totalDeposited = 0m;
        var totalWithdrawn = 0m;

        for (int i = 0; i < numberOfTransactions; i++)
        {
            var transactionId = Guid.NewGuid().ToString();
            var amount = Money.Create(baseAmount + (i * 10m), currency);

            if (i % 3 == 0)
            {
                await grain.WithdrawAsync(Money.Create(Math.Min(amount.Amount, totalDeposited - totalWithdrawn), currency), transactionId);
                totalWithdrawn += Math.Min(amount.Amount, totalDeposited - totalWithdrawn);
            }
            else
            {
                var result = await grain.DepositAsync(amount, transactionId);
                result.IsSuccess.Should().BeTrue($"Deposit {i} should succeed");
                totalDeposited += amount.Amount;
            }

            await Task.Delay(1);
        }

        return grain;
    }

    public static async Task AssertBalanceConsistencyAsync(
        IUserWalletGrain grain,
        Money expectedBalance)
    {
        var actualBalance = await grain.GetBalanceAsync();
        actualBalance.Should().Be(expectedBalance, "Wallet balance should match expected value");
    }

    public static async Task AssertAvailableBalanceAsync(
        IUserWalletGrain grain,
        Money expectedAvailableBalance)
    {
        var actualAvailableBalance = await grain.GetAvailableBalanceAsync();
        actualAvailableBalance.Should().Be(expectedAvailableBalance, "Available balance should match expected value");
    }

    public static async Task AssertTransactionExistsAsync(
        IUserWalletGrain grain,
        TransactionType expectedType,
        Money expectedAmount,
        TransactionStatus expectedStatus)
    {
        var transactions = await grain.GetTransactionHistoryAsync(50);
        
        var matchingTransaction = transactions.FirstOrDefault(t =>
            t.Type == expectedType &&
            t.Amount == expectedAmount &&
            t.Status == expectedStatus);

        matchingTransaction.Should().NotBeNull(
            $"Should find transaction of type {expectedType} with amount {expectedAmount.Amount} {expectedAmount.Currency} and status {expectedStatus}");
    }

    public static async Task AssertLedgerBalanceAsync(IUserWalletGrain grain)
    {
        var ledgerEntries = await grain.GetLedgerEntriesAsync(1000);
        
        var totalCredits = ledgerEntries
            .Where(e => e.Type == EntryType.Credit)
            .Sum(e => e.Amount.Amount);
            
        var totalDebits = ledgerEntries
            .Where(e => e.Type == EntryType.Debit)
            .Sum(e => e.Amount.Amount);

        totalCredits.Should().Be(totalDebits, "Total credits should equal total debits in double-entry bookkeeping");
    }

    public static void AssertTransactionResult(
        TransactionResult result,
        bool expectedSuccess,
        TransactionType? expectedType = null,
        Money? expectedAmount = null,
        string? expectedErrorMessage = null)
    {
        result.IsSuccess.Should().Be(expectedSuccess, "Transaction result success flag should match expected");

        if (expectedSuccess)
        {
            result.Transaction.Should().NotBeNull("Successful transaction should have transaction details");
            result.NewBalance.Should().NotBeNull("Successful transaction should have new balance");
            
            if (expectedType.HasValue && result.Transaction.HasValue)
            {
                result.Transaction.Value.Type.Should().Be(expectedType.Value, "Transaction type should match expected");
            }
            
            if (expectedAmount.HasValue && result.Transaction.HasValue)
            {
                result.Transaction.Value.Amount.Should().Be(expectedAmount.Value, "Transaction amount should match expected");
            }
        }
        else
        {
            result.ErrorMessage.Should().NotBeNullOrEmpty("Failed transaction should have error message");
            
            if (!string.IsNullOrEmpty(expectedErrorMessage))
            {
                result.ErrorMessage.Should().Be(expectedErrorMessage, "Error message should match expected");
            }
        }
    }

    public static void AssertMoney(Money actual, decimal expectedAmount, string expectedCurrency = "USD")
    {
        actual.Amount.Should().Be(expectedAmount, "Money amount should match expected");
        actual.Currency.Should().Be(expectedCurrency, "Money currency should match expected");
    }

    public static void AssertTransaction(
        WalletTransaction transaction,
        string expectedUserId,
        TransactionType expectedType,
        Money expectedAmount,
        TransactionStatus expectedStatus,
        string? expectedReferenceId = null)
    {
        transaction.Id.Should().NotBeNullOrEmpty("Transaction should have an ID");
        transaction.UserId.Should().Be(expectedUserId, "Transaction user ID should match expected");
        transaction.Type.Should().Be(expectedType, "Transaction type should match expected");
        transaction.Amount.Should().Be(expectedAmount, "Transaction amount should match expected");
        transaction.Status.Should().Be(expectedStatus, "Transaction status should match expected");
        transaction.Description.Should().NotBeNullOrEmpty("Transaction should have a description");
        transaction.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), "Transaction timestamp should be recent");
        
        if (expectedReferenceId != null)
        {
            transaction.ReferenceId.Should().Be(expectedReferenceId, "Transaction reference ID should match expected");
        }
    }

    public static void AssertTransactionEntry(
        TransactionEntry entry,
        string expectedTransactionId,
        Money expectedAmount,
        EntryType expectedType,
        string expectedDescriptionContains)
    {
        entry.Id.Should().NotBeNullOrEmpty("Entry should have an ID");
        entry.TransactionId.Should().Be(expectedTransactionId, "Entry transaction ID should match expected");
        entry.Amount.Should().Be(expectedAmount, "Entry amount should match expected");
        entry.Type.Should().Be(expectedType, "Entry type should match expected");
        entry.Description.Should().Contain(expectedDescriptionContains, "Entry description should contain expected text");
        entry.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), "Entry timestamp should be recent");
    }

    public static async Task<List<WalletTransaction>> GetAllTransactionsAsync(IUserWalletGrain grain)
    {
        return (await grain.GetTransactionHistoryAsync(1000)).ToList();
    }

    public static async Task<List<TransactionEntry>> GetAllLedgerEntriesAsync(IUserWalletGrain grain)
    {
        return (await grain.GetLedgerEntriesAsync(1000)).ToList();
    }

    public static async Task<Money> CalculateExpectedBalanceFromTransactionsAsync(IUserWalletGrain grain)
    {
        var transactions = await GetAllTransactionsAsync(grain);
        var completedTransactions = transactions.Where(t => t.Status == TransactionStatus.Completed);

        var totalDeposits = completedTransactions
            .Where(t => t.Type == TransactionType.Deposit || t.Type == TransactionType.BetWin || t.Type == TransactionType.BetRefund)
            .Sum(t => t.Amount.Amount);

        var totalWithdrawals = completedTransactions
            .Where(t => t.Type == TransactionType.Withdrawal || t.Type == TransactionType.ReservationCommit)
            .Sum(t => t.Amount.Amount);

        var netAmount = totalDeposits - totalWithdrawals;
        var currency = transactions.Any() ? transactions.First().Amount.Currency : "USD";

        return Money.Create(netAmount, currency);
    }

    public static class Scenarios
    {
        public static async Task<IUserWalletGrain> CreateHighVolumeWalletAsync(
            IGrainFactory grainFactory,
            string userId,
            int transactionCount = 100)
        {
            return await SetupWalletWithTransactionHistoryAsync(
                grainFactory, 
                userId, 
                transactionCount,
                50m);
        }

        public static async Task<IUserWalletGrain> CreateWalletWithReservationsAsync(
            IGrainFactory grainFactory,
            string userId,
            decimal initialBalance = 1000m,
            int reservationCount = 3)
        {
            var grain = await SetupWalletWithBalanceAsync(grainFactory, userId, initialBalance);

            for (int i = 0; i < reservationCount; i++)
            {
                var betId = Guid.NewGuid().ToString();
                var reservationAmount = Money.Create(50m + (i * 25m));
                await grain.ReserveAsync(reservationAmount, betId);
            }

            return grain;
        }

        public static async Task<IUserWalletGrain> CreateEmptyWalletAsync(
            IGrainFactory grainFactory,
            string userId)
        {
            return grainFactory.GetGrain<IUserWalletGrain>(userId);
        }
    }

    public static class Assertions
    {
        public static void ReservationShouldSucceed(TransactionResult result, Money expectedReservationAmount)
        {
            AssertTransactionResult(result, true, TransactionType.Reservation, expectedReservationAmount);
        }

        public static void ReservationShouldFail(TransactionResult result, string expectedError)
        {
            AssertTransactionResult(result, false, expectedErrorMessage: expectedError);
        }

        public static void DepositShouldSucceed(TransactionResult result, Money expectedDepositAmount)
        {
            AssertTransactionResult(result, true, TransactionType.Deposit, expectedDepositAmount);
        }

        public static void WithdrawalShouldSucceed(TransactionResult result, Money expectedWithdrawalAmount)
        {
            AssertTransactionResult(result, true, TransactionType.Withdrawal, expectedWithdrawalAmount);
        }

        public static void WithdrawalShouldFail(TransactionResult result, string expectedError = "Insufficient available balance")
        {
            AssertTransactionResult(result, false, expectedErrorMessage: expectedError);
        }
    }
}