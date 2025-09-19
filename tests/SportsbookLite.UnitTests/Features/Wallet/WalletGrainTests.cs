using FluentAssertions;
using SportsbookLite.Contracts.Wallet;
using SportsbookLite.Grains.Wallet;
using SportsbookLite.GrainInterfaces;
using SportsbookLite.TestUtilities;
using Orleans.TestingHost;

namespace SportsbookLite.UnitTests.Features.Wallet;

public class WalletGrainTests : OrleansTestBase
{
    private TestCluster _cluster = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        var builder = new TestClusterBuilder()
            .AddSiloBuilderConfigurator<SiloConfigurator>();

        _cluster = builder.Build();
        await _cluster.DeployAsync();
    }

    public override async Task DisposeAsync()
    {
        if (_cluster != null)
        {
            await _cluster.DisposeAsync();
        }
        await base.DisposeAsync();
    }

    private class SiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorage("Default");
        }
    }

    [Fact]
    public async Task DepositAsync_WithValidAmount_ShouldSucceed()
    {
        var userId = "user123";
        var transactionId = Guid.NewGuid().ToString();
        var amount = Money.Create(100.50m);

        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        var result = await grain.DepositAsync(amount, transactionId);

        result.IsSuccess.Should().BeTrue();
        result.Transaction.Should().NotBeNull();
        result.Transaction!.Value.Type.Should().Be(TransactionType.Deposit);
        result.Transaction.Value.Amount.Should().Be(amount);
        result.Transaction.Value.Status.Should().Be(TransactionStatus.Completed);
        result.NewBalance.Should().NotBeNull();
        result.NewBalance!.Value.Should().Be(amount);
    }

    [Fact]
    public async Task DepositAsync_WithNegativeAmount_ShouldFail()
    {
        var userId = "user124";
        var transactionId = Guid.NewGuid().ToString();

        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        var result = await grain.DepositAsync(Money.Zero(), transactionId);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Deposit amount must be positive");
    }

    [Fact]
    public async Task DepositAsync_WithDuplicateTransactionId_ShouldBeIdempotent()
    {
        var userId = "user125";
        var transactionId = Guid.NewGuid().ToString();
        var amount = Money.Create(50.25m);

        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        var firstResult = await grain.DepositAsync(amount, transactionId);
        var secondResult = await grain.DepositAsync(amount, transactionId);

        firstResult.IsSuccess.Should().BeTrue();
        secondResult.IsSuccess.Should().BeTrue();
        firstResult.Transaction!.Value.ReferenceId.Should().Be(transactionId);
        secondResult.Transaction!.Value.ReferenceId.Should().Be(transactionId);
        firstResult.NewBalance.Should().Be(secondResult.NewBalance);
    }

    [Fact]
    public async Task WithdrawAsync_WithSufficientFunds_ShouldSucceed()
    {
        var userId = "user126";
        var depositTransactionId = Guid.NewGuid().ToString();
        var withdrawTransactionId = Guid.NewGuid().ToString();
        var depositAmount = Money.Create(200m);
        var withdrawAmount = Money.Create(75m);

        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        await grain.DepositAsync(depositAmount, depositTransactionId);
        var result = await grain.WithdrawAsync(withdrawAmount, withdrawTransactionId);

        result.IsSuccess.Should().BeTrue();
        result.Transaction!.Value.Type.Should().Be(TransactionType.Withdrawal);
        result.Transaction.Value.Amount.Should().Be(withdrawAmount);
        result.Transaction.Value.Status.Should().Be(TransactionStatus.Completed);
        result.NewBalance.Should().Be(Money.Create(125m));
    }

    [Fact]
    public async Task WithdrawAsync_WithInsufficientFunds_ShouldFail()
    {
        var userId = "user127";
        var transactionId = Guid.NewGuid().ToString();
        var withdrawAmount = Money.Create(100m);

        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        var result = await grain.WithdrawAsync(withdrawAmount, transactionId);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Insufficient available balance");
    }

    [Fact]
    public async Task WithdrawAsync_WithNegativeAmount_ShouldFail()
    {
        var userId = "user128";
        var transactionId = Guid.NewGuid().ToString();

        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        var result = await grain.WithdrawAsync(Money.Zero(), transactionId);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Withdrawal amount must be positive");
    }

    [Fact]
    public async Task ReserveAsync_WithSufficientBalance_ShouldSucceed()
    {
        var userId = "user129";
        var depositTransactionId = Guid.NewGuid().ToString();
        var betId = Guid.NewGuid().ToString();
        var depositAmount = Money.Create(150m);
        var reservationAmount = Money.Create(50m);

        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        await grain.DepositAsync(depositAmount, depositTransactionId);
        var result = await grain.ReserveAsync(reservationAmount, betId);

        result.IsSuccess.Should().BeTrue();
        result.Transaction!.Value.Type.Should().Be(TransactionType.Reservation);
        result.Transaction.Value.Amount.Should().Be(reservationAmount);
        result.NewBalance.Should().Be(Money.Create(100m));

        var availableBalance = await grain.GetAvailableBalanceAsync();
        availableBalance.Should().Be(Money.Create(100m));

        var totalBalance = await grain.GetBalanceAsync();
        totalBalance.Should().Be(Money.Create(150m));
    }

    [Fact]
    public async Task ReserveAsync_WithInsufficientBalance_ShouldFail()
    {
        var userId = "user130";
        var betId = Guid.NewGuid().ToString();
        var reservationAmount = Money.Create(100m);

        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        var result = await grain.ReserveAsync(reservationAmount, betId);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Insufficient available balance for reservation");
    }

    [Fact]
    public async Task ReserveAsync_WithDuplicateBetId_ShouldFail()
    {
        var userId = "user131";
        var depositTransactionId = Guid.NewGuid().ToString();
        var betId = Guid.NewGuid().ToString();
        var depositAmount = Money.Create(200m);
        var reservationAmount = Money.Create(50m);

        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        await grain.DepositAsync(depositAmount, depositTransactionId);
        await grain.ReserveAsync(reservationAmount, betId);
        var result = await grain.ReserveAsync(reservationAmount, betId);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be($"Reservation for bet {betId} already exists");
    }

    [Fact]
    public async Task CommitReservationAsync_WithValidReservation_ShouldSucceed()
    {
        var userId = "user132";
        var depositTransactionId = Guid.NewGuid().ToString();
        var betId = Guid.NewGuid().ToString();
        var depositAmount = Money.Create(100m);
        var reservationAmount = Money.Create(30m);

        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        await grain.DepositAsync(depositAmount, depositTransactionId);
        await grain.ReserveAsync(reservationAmount, betId);
        var result = await grain.CommitReservationAsync(betId);

        result.IsSuccess.Should().BeTrue();
        result.Transaction!.Value.Type.Should().Be(TransactionType.ReservationCommit);
        result.NewBalance.Should().Be(Money.Create(70m));

        var availableBalance = await grain.GetAvailableBalanceAsync();
        availableBalance.Should().Be(Money.Create(70m));
    }

    [Fact]
    public async Task CommitReservationAsync_WithoutReservation_ShouldFail()
    {
        var userId = "user133";
        var betId = Guid.NewGuid().ToString();

        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        var result = await grain.CommitReservationAsync(betId);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be($"No reservation found for bet {betId}");
    }

    [Fact]
    public async Task ReleaseReservationAsync_WithValidReservation_ShouldSucceed()
    {
        var userId = "user134";
        var depositTransactionId = Guid.NewGuid().ToString();
        var betId = Guid.NewGuid().ToString();
        var depositAmount = Money.Create(100m);
        var reservationAmount = Money.Create(40m);

        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        await grain.DepositAsync(depositAmount, depositTransactionId);
        await grain.ReserveAsync(reservationAmount, betId);
        var result = await grain.ReleaseReservationAsync(betId);

        result.IsSuccess.Should().BeTrue();
        result.Transaction!.Value.Type.Should().Be(TransactionType.ReservationRelease);
        result.NewBalance.Should().Be(Money.Create(100m));

        var availableBalance = await grain.GetAvailableBalanceAsync();
        availableBalance.Should().Be(Money.Create(100m));
    }

    [Fact]
    public async Task ReleaseReservationAsync_WithoutReservation_ShouldFail()
    {
        var userId = "user135";
        var betId = Guid.NewGuid().ToString();

        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        var result = await grain.ReleaseReservationAsync(betId);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be($"No reservation found for bet {betId}");
    }

    [Fact]
    public async Task GetTransactionHistoryAsync_ShouldReturnOrderedTransactions()
    {
        var userId = "user136";
        var depositTransactionId = Guid.NewGuid().ToString();
        var withdrawTransactionId = Guid.NewGuid().ToString();
        var depositAmount = Money.Create(200m);
        var withdrawAmount = Money.Create(50m);

        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        await grain.DepositAsync(depositAmount, depositTransactionId);
        await Task.Delay(10);
        await grain.WithdrawAsync(withdrawAmount, withdrawTransactionId);

        var transactions = await grain.GetTransactionHistoryAsync();

        transactions.Should().HaveCount(2);
        transactions[0].Type.Should().Be(TransactionType.Withdrawal);
        transactions[1].Type.Should().Be(TransactionType.Deposit);
        transactions.All(t => t.Status == TransactionStatus.Completed).Should().BeTrue();
    }

    [Fact]
    public async Task GetLedgerEntriesAsync_ShouldReturnDoubleEntryBookkeeping()
    {
        var userId = "user137";
        var transactionId = Guid.NewGuid().ToString();
        var amount = Money.Create(100m);

        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        await grain.DepositAsync(amount, transactionId);

        var ledgerEntries = await grain.GetLedgerEntriesAsync();

        ledgerEntries.Should().HaveCount(2);

        var creditEntry = ledgerEntries.SingleOrDefault(e => e.Type == EntryType.Credit);
        var debitEntry = ledgerEntries.SingleOrDefault(e => e.Type == EntryType.Debit);

        creditEntry.Should().NotBeNull();
        creditEntry!.Amount.Should().Be(amount);
        creditEntry.Description.Should().Contain("Deposit credit");

        debitEntry.Should().NotBeNull();
        debitEntry!.Amount.Should().Be(amount);
        debitEntry.Description.Should().Contain("External deposit debit");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task GetTransactionHistoryAsync_WithLimit_ShouldRespectLimit(int limit)
    {
        var userId = $"user138_{limit}";
        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        for (int i = 0; i < 15; i++)
        {
            var transactionId = Guid.NewGuid().ToString();
            var amount = Money.Create(10m + i);
            await grain.DepositAsync(amount, transactionId);
        }

        var transactions = await grain.GetTransactionHistoryAsync(limit);

        transactions.Should().HaveCount(Math.Min(limit, 15));
    }

    [Fact]
    public async Task WalletOperations_ShouldMaintainBalanceConsistency()
    {
        var userId = "user139";
        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        var deposit1Id = Guid.NewGuid().ToString();
        var deposit2Id = Guid.NewGuid().ToString();
        var withdrawId = Guid.NewGuid().ToString();
        var betId1 = Guid.NewGuid().ToString();
        var betId2 = Guid.NewGuid().ToString();

        await grain.DepositAsync(Money.Create(1000m), deposit1Id);
        await grain.DepositAsync(Money.Create(500m), deposit2Id);

        var balanceAfterDeposits = await grain.GetBalanceAsync();
        balanceAfterDeposits.Should().Be(Money.Create(1500m));

        await grain.WithdrawAsync(Money.Create(200m), withdrawId);

        var balanceAfterWithdraw = await grain.GetBalanceAsync();
        balanceAfterWithdraw.Should().Be(Money.Create(1300m));

        await grain.ReserveAsync(Money.Create(300m), betId1);
        await grain.ReserveAsync(Money.Create(150m), betId2);

        var totalBalance = await grain.GetBalanceAsync();
        var availableBalance = await grain.GetAvailableBalanceAsync();

        totalBalance.Should().Be(Money.Create(1300m));
        availableBalance.Should().Be(Money.Create(850m));

        await grain.CommitReservationAsync(betId1);

        var balanceAfterCommit = await grain.GetBalanceAsync();
        var availableAfterCommit = await grain.GetAvailableBalanceAsync();

        balanceAfterCommit.Should().Be(Money.Create(1000m));
        availableAfterCommit.Should().Be(Money.Create(850m));

        await grain.ReleaseReservationAsync(betId2);

        var finalBalance = await grain.GetBalanceAsync();
        var finalAvailable = await grain.GetAvailableBalanceAsync();

        finalBalance.Should().Be(Money.Create(1000m));
        finalAvailable.Should().Be(Money.Create(1000m));
    }
}