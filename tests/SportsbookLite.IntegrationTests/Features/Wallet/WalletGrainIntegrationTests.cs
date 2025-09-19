using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.TestingHost;
using SportsbookLite.Contracts.Wallet;
using SportsbookLite.GrainInterfaces;
using SportsbookLite.Grains.Wallet;
using SportsbookLite.TestUtilities;

namespace SportsbookLite.IntegrationTests.Features.Wallet;

public class WalletGrainIntegrationTests : BaseIntegrationTest
{
    private TestCluster _cluster = null!;

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

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
    public async Task GrainPersistence_ShouldMaintainStateAcrossActivations()
    {
        var userId = "persistence_user_001";
        var grain1 = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);
        var transactionId = Guid.NewGuid().ToString();
        var amount = Money.Create(250m);

        await grain1.DepositAsync(amount, transactionId);
        var balance1 = await grain1.GetBalanceAsync();

        var grain2 = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);
        var balance2 = await grain2.GetBalanceAsync();

        balance1.Should().Be(balance2);
        balance2.Should().Be(amount);
    }

    [Fact]
    public async Task ConcurrentOperations_OnSameGrain_ShouldBeThreadSafe()
    {
        var userId = "concurrent_user_001";
        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);
        var numberOfOperations = 10;
        var depositAmount = Money.Create(10m);

        var tasks = new List<Task<TransactionResult>>();
        for (int i = 0; i < numberOfOperations; i++)
        {
            var transactionId = Guid.NewGuid().ToString();
            tasks.Add(grain.DepositAsync(depositAmount, transactionId).AsTask());
        }

        var results = await Task.WhenAll(tasks);

        results.All(r => r.IsSuccess).Should().BeTrue();

        var finalBalance = await grain.GetBalanceAsync();
        finalBalance.Should().Be(Money.Create(numberOfOperations * 10m));

        var transactions = await grain.GetTransactionHistoryAsync();
        transactions.Should().HaveCount(numberOfOperations);
        transactions.All(t => t.Status == TransactionStatus.Completed).Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentReservations_ShouldPreventOverspending()
    {
        var userId = "reservation_user_001";
        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);
        var depositTransactionId = Guid.NewGuid().ToString();
        var depositAmount = Money.Create(100m);
        var reservationAmount = Money.Create(60m);

        await grain.DepositAsync(depositAmount, depositTransactionId);

        var reservationTasks = new List<Task<TransactionResult>>();
        for (int i = 0; i < 3; i++)
        {
            var betId = Guid.NewGuid().ToString();
            reservationTasks.Add(grain.ReserveAsync(reservationAmount, betId).AsTask());
        }

        var results = await Task.WhenAll(reservationTasks);

        var successfulReservations = results.Count(r => r.IsSuccess);
        var failedReservations = results.Count(r => !r.IsSuccess);

        successfulReservations.Should().Be(1);
        failedReservations.Should().Be(2);

        var availableBalance = await grain.GetAvailableBalanceAsync();
        availableBalance.Should().Be(Money.Create(40m));

        var totalBalance = await grain.GetBalanceAsync();
        totalBalance.Should().Be(Money.Create(100m));
    }

    [Fact]
    public async Task ComplexWorkflow_WithReservationLifecycle_ShouldWorkCorrectly()
    {
        var userId = "workflow_user_001";
        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);
        
        var depositTransactionId = Guid.NewGuid().ToString();
        var betId1 = Guid.NewGuid().ToString();
        var betId2 = Guid.NewGuid().ToString();
        var betId3 = Guid.NewGuid().ToString();

        await grain.DepositAsync(Money.Create(1000m), depositTransactionId);

        await grain.ReserveAsync(Money.Create(200m), betId1);
        await grain.ReserveAsync(Money.Create(300m), betId2);
        await grain.ReserveAsync(Money.Create(150m), betId3);

        var balanceAfterReservations = await grain.GetBalanceAsync();
        var availableAfterReservations = await grain.GetAvailableBalanceAsync();

        balanceAfterReservations.Should().Be(Money.Create(1000m));
        availableAfterReservations.Should().Be(Money.Create(350m));

        await grain.CommitReservationAsync(betId1);
        await grain.ReleaseReservationAsync(betId2);

        var balanceAfterCommitAndRelease = await grain.GetBalanceAsync();
        var availableAfterCommitAndRelease = await grain.GetAvailableBalanceAsync();

        balanceAfterCommitAndRelease.Should().Be(Money.Create(800m));
        availableAfterCommitAndRelease.Should().Be(Money.Create(650m));

        await grain.ReleaseReservationAsync(betId3);

        var finalBalance = await grain.GetBalanceAsync();
        var finalAvailable = await grain.GetAvailableBalanceAsync();

        finalBalance.Should().Be(Money.Create(800m));
        finalAvailable.Should().Be(Money.Create(800m));

        var transactions = await grain.GetTransactionHistoryAsync();
        var ledgerEntries = await grain.GetLedgerEntriesAsync();

        transactions.Should().HaveCountGreaterThan(3);
        ledgerEntries.Should().HaveCountGreaterThan(0);

        var commitTransaction = transactions.FirstOrDefault(t => t.Type == TransactionType.ReservationCommit);
        commitTransaction.Should().NotBeNull();
        commitTransaction!.Amount.Should().Be(Money.Create(200m));
    }

    [Fact]
    public async Task EventPublishing_ShouldNotFailGrainOperations()
    {
        var userId = "event_user_001";
        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);
        var transactionId = Guid.NewGuid().ToString();
        var amount = Money.Create(100m);

        var result = await grain.DepositAsync(amount, transactionId);

        result.IsSuccess.Should().BeTrue();
        result.Transaction.Should().NotBeNull();
        result.NewBalance.Should().Be(amount);
    }

    [Fact]
    public async Task LedgerEntries_ShouldFollowDoubleEntryBookkeeping()
    {
        var userId = "ledger_user_001";
        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);
        
        var depositTransactionId = Guid.NewGuid().ToString();
        var withdrawTransactionId = Guid.NewGuid().ToString();
        var depositAmount = Money.Create(500m);
        var withdrawAmount = Money.Create(200m);

        await grain.DepositAsync(depositAmount, depositTransactionId);
        await grain.WithdrawAsync(withdrawAmount, withdrawTransactionId);

        var ledgerEntries = await grain.GetLedgerEntriesAsync();

        var depositEntries = ledgerEntries.Where(e => e.TransactionId == depositTransactionId).ToList();
        var withdrawEntries = ledgerEntries.Where(e => e.TransactionId == withdrawTransactionId).ToList();

        depositEntries.Should().HaveCount(2);
        withdrawEntries.Should().HaveCount(2);

        var depositCredit = depositEntries.Single(e => e.Type == EntryType.Credit);
        var depositDebit = depositEntries.Single(e => e.Type == EntryType.Debit);

        depositCredit.Amount.Should().Be(depositAmount);
        depositDebit.Amount.Should().Be(depositAmount);

        var withdrawCredit = withdrawEntries.Single(e => e.Type == EntryType.Credit);
        var withdrawDebit = withdrawEntries.Single(e => e.Type == EntryType.Debit);

        withdrawCredit.Amount.Should().Be(withdrawAmount);
        withdrawDebit.Amount.Should().Be(withdrawAmount);

        var totalCredits = ledgerEntries.Where(e => e.Type == EntryType.Credit).Sum(e => e.Amount.Amount);
        var totalDebits = ledgerEntries.Where(e => e.Type == EntryType.Debit).Sum(e => e.Amount.Amount);

        totalCredits.Should().Be(totalDebits);
    }

    [Fact]
    public async Task LargeNumberOfTransactions_ShouldPerformWell()
    {
        var userId = "performance_user_001";
        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);
        var numberOfTransactions = 100;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var tasks = new List<Task<TransactionResult>>();
        for (int i = 0; i < numberOfTransactions; i++)
        {
            var transactionId = Guid.NewGuid().ToString();
            var amount = Money.Create(1m + (i * 0.01m));
            tasks.Add(grain.DepositAsync(amount, transactionId).AsTask());
        }

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        results.All(r => r.IsSuccess).Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000);

        var finalBalance = await grain.GetBalanceAsync();
        var expectedTotal = Enumerable.Range(0, numberOfTransactions)
            .Sum(i => 1m + (i * 0.01m));
        
        finalBalance.Amount.Should().Be(expectedTotal);

        var transactions = await grain.GetTransactionHistoryAsync(numberOfTransactions);
        transactions.Should().HaveCount(numberOfTransactions);
    }

    [Fact]
    public async Task MultipleGrains_ShouldOperateIndependently()
    {
        var user1Id = "multi_user_001";
        var user2Id = "multi_user_002";
        var grain1 = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(user1Id);
        var grain2 = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(user2Id);

        var user1Deposit = Money.Create(100m);
        var user2Deposit = Money.Create(200m);

        await grain1.DepositAsync(user1Deposit, Guid.NewGuid().ToString());
        await grain2.DepositAsync(user2Deposit, Guid.NewGuid().ToString());

        var balance1 = await grain1.GetBalanceAsync();
        var balance2 = await grain2.GetBalanceAsync();

        balance1.Should().Be(user1Deposit);
        balance2.Should().Be(user2Deposit);

        var transactions1 = await grain1.GetTransactionHistoryAsync();
        var transactions2 = await grain2.GetTransactionHistoryAsync();

        transactions1.Should().HaveCount(1);
        transactions2.Should().HaveCount(1);
        transactions1.Single().UserId.Should().Be(user1Id);
        transactions2.Single().UserId.Should().Be(user2Id);
    }

    [Fact]
    public async Task ErrorRecovery_AfterFailedOperation_ShouldNotCorruptState()
    {
        var userId = "error_user_001";
        var grain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);
        
        var depositResult = await grain.DepositAsync(Money.Create(100m), Guid.NewGuid().ToString());
        depositResult.IsSuccess.Should().BeTrue();

        var withdrawResult = await grain.WithdrawAsync(Money.Create(200m), Guid.NewGuid().ToString());
        withdrawResult.IsSuccess.Should().BeFalse();

        var balanceAfterFailedWithdraw = await grain.GetBalanceAsync();
        balanceAfterFailedWithdraw.Should().Be(Money.Create(100m));

        var successfulWithdrawResult = await grain.WithdrawAsync(Money.Create(50m), Guid.NewGuid().ToString());
        successfulWithdrawResult.IsSuccess.Should().BeTrue();

        var finalBalance = await grain.GetBalanceAsync();
        finalBalance.Should().Be(Money.Create(50m));
    }
}