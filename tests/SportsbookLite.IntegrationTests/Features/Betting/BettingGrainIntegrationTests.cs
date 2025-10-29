using FluentAssertions;
using Orleans.TestingHost;
using SportsbookLite.Contracts.Betting;
using SportsbookLite.Contracts.Events;
using SportsbookLite.Contracts.Odds;
using SportsbookLite.Contracts.Wallet;
using SportsbookLite.GrainInterfaces;
using SportsbookLite.Grains.Betting;
using SportsbookLite.Grains.Events;
using SportsbookLite.Grains.Odds;
using SportsbookLite.Grains.Wallet;
using SportsbookLite.Infrastructure.EventStore;
using SportsbookLite.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace SportsbookLite.IntegrationTests.Features.Betting;

public class BettingGrainIntegrationTests : BaseIntegrationTest
{
    private TestCluster _cluster = null!;
    private IEventStore _eventStore = null!;

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        _eventStore = Substitute.For<IEventStore>();
        services.AddSingleton(_eventStore);
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
            
            siloBuilder.ConfigureServices(services =>
            {
                services.AddSingleton<IEventStore>(provider => 
                {
                    var eventStore = Substitute.For<IEventStore>();
                    eventStore.SaveEventsAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<IDomainEvent>>())
                        .Returns(ValueTask.CompletedTask);
                    eventStore.GetEventStreamAsync(Arg.Any<string>())
                        .Returns((EventStream?)null);
                    eventStore.GetEventsAsync(Arg.Any<string>())
                        .Returns(new List<IDomainEvent>());
                    return eventStore;
                });
            });
        }
    }

    [Fact]
    public async Task FullBettingWorkflow_WithAllGrains_ShouldSucceed()
    {
        var userId = "integration_user_001";
        var eventId = Guid.NewGuid();
        var marketId = "match_winner";
        var selectionId = "home_win";
        var betId = Guid.NewGuid();

        var walletGrain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);
        var eventGrain = _cluster.GrainFactory.GetGrain<ISportEventGrain>(eventId);
        var oddsGrain = _cluster.GrainFactory.GetGrain<IOddsGrain>(marketId);
        var betGrain = _cluster.GrainFactory.GetGrain<IBetGrain>(betId);
        var betManagerGrain = _cluster.GrainFactory.GetGrain<IBetManagerGrain>(userId);

        await walletGrain.DepositAsync(Money.Create(500m), "initial_deposit");

        var sportEvent = new SportEvent(
            eventId,
            "Manchester United vs Liverpool",
            SportType.Football,
            "Premier League",
            DateTimeOffset.UtcNow.AddHours(2),
            null,
            EventStatus.Scheduled,
            new Dictionary<string, string>
            {
                ["home"] = "Manchester United",
                ["away"] = "Liverpool"
            },
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        );
        await eventGrain.CreateAsync(sportEvent);

        await eventGrain.AddMarketAsync(
            "Match Winner",
            "Match result - Home, Draw, or Away",
            new Dictionary<string, decimal>
            {
                ["home_win"] = 2.5m,
                ["draw"] = 3.0m,
                ["away_win"] = 2.8m
            }
        );

        var oddsUpdate = new OddsUpdateRequest(
            marketId.ToString(),
            new Dictionary<string, decimal>
            {
                ["home_win"] = 2.5m,
                ["draw"] = 3.2m,
                ["away_win"] = 2.8m
            },
            OddsSource.Manual,
            null,
            null,
            DateTimeOffset.UtcNow
        );
        await oddsGrain.UpdateOddsAsync(oddsUpdate);

        var placeBetRequest = BettingTestHelpers.CreateValidPlaceBetRequest(
            betId, userId, eventId, marketId, selectionId, 
            amount: 100m, acceptableOdds: 2.0m);

        var betResult = await betGrain.PlaceBetAsync(placeBetRequest);

        betResult.IsSuccess.Should().BeTrue();
        betResult.Bet.Should().NotBeNull();
        betResult.Bet!.Id.Should().Be(betId);
        betResult.Bet.UserId.Should().Be(userId);
        betResult.Bet.Status.Should().Be(BetStatus.Accepted);
        betResult.Bet.Amount.Should().Be(Money.Create(100m));

        await betManagerGrain.AddBetAsync(betId);

        var userBets = await betManagerGrain.GetUserBetsAsync();
        userBets.Should().HaveCount(1);
        userBets[0].Id.Should().Be(betId);

        var walletBalance = await walletGrain.GetBalanceAsync();
        var availableBalance = await walletGrain.GetAvailableBalanceAsync();
        walletBalance.Should().Be(Money.Create(400m));
        availableBalance.Should().Be(Money.Create(400m));

        var activeBets = await betManagerGrain.GetActiveBetsAsync();
        activeBets.Should().HaveCount(1);
        activeBets[0].Id.Should().Be(betId);
    }

    [Fact]
    public async Task ConcurrentBetPlacement_ShouldMaintainConsistency()
    {
        var userId = "integration_user_002";
        var eventId = Guid.NewGuid();
        var marketId = "concurrent_market";
        var selectionId = "selection_1";

        await SetupEventAndOddsForTesting(eventId, marketId, selectionId, 2.0m);

        var walletGrain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);
        await walletGrain.DepositAsync(Money.Create(1000m), "initial_deposit");

        var betTasks = new List<Task<BetResult>>();
        var betIds = new List<Guid>();

        for (int i = 0; i < 5; i++)
        {
            var betId = Guid.NewGuid();
            betIds.Add(betId);
            
            var betGrain = _cluster.GrainFactory.GetGrain<IBetGrain>(betId);
            var request = BettingTestHelpers.CreateValidPlaceBetRequest(
                betId, userId, eventId, marketId, selectionId, 
                amount: 100m, acceptableOdds: 1.5m);
            
            betTasks.Add(betGrain.PlaceBetAsync(request).AsTask());
        }

        var results = await Task.WhenAll(betTasks);

        var successfulBets = results.Count(r => r.IsSuccess);
        var failedBets = results.Count(r => !r.IsSuccess);

        successfulBets.Should().BeGreaterThan(0);
        failedBets.Should().BeGreaterThan(0);

        var finalBalance = await walletGrain.GetBalanceAsync();
        finalBalance.Amount.Should().Be(1000m - (successfulBets * 100m));

        var betManagerGrain = _cluster.GrainFactory.GetGrain<IBetManagerGrain>(userId);
        foreach (var betId in betIds.Where((_, i) => results[i].IsSuccess))
        {
            await betManagerGrain.AddBetAsync(betId);
        }

        var userBets = await betManagerGrain.GetUserBetsAsync();
        userBets.Should().HaveCount(successfulBets);
    }

    [Fact]
    public async Task BetVoidingWorkflow_ShouldReleaseReservations()
    {
        var userId = "integration_user_003";
        var eventId = Guid.NewGuid();
        var marketId = "void_market";
        var selectionId = "void_selection";
        var betId = Guid.NewGuid();

        await SetupCompleteScenario(userId, eventId, marketId, selectionId, betId);

        var betGrain = _cluster.GrainFactory.GetGrain<IBetGrain>(betId);
        var walletGrain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        var balanceBeforeVoid = await walletGrain.GetBalanceAsync();
        var availableBeforeVoid = await walletGrain.GetAvailableBalanceAsync();

        var voidResult = await betGrain.VoidBetAsync("Event cancelled");

        voidResult.IsSuccess.Should().BeTrue();
        voidResult.Bet!.Status.Should().Be(BetStatus.Void);
        voidResult.Bet.VoidReason.Should().Be("Event cancelled");

        var balanceAfterVoid = await walletGrain.GetBalanceAsync();
        var availableAfterVoid = await walletGrain.GetAvailableBalanceAsync();

        balanceAfterVoid.Should().Be(Money.Create(500m));
        availableAfterVoid.Should().Be(Money.Create(500m));
    }

    [Fact]
    public async Task CashOutWorkflow_ShouldProcessCorrectly()
    {
        var userId = "integration_user_004";
        var eventId = Guid.NewGuid();
        var marketId = "cashout_market";
        var selectionId = "cashout_selection";
        var betId = Guid.NewGuid();

        await SetupCompleteScenario(userId, eventId, marketId, selectionId, betId);

        var betGrain = _cluster.GrainFactory.GetGrain<IBetGrain>(betId);
        var walletGrain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);

        var balanceBeforeCashOut = await walletGrain.GetBalanceAsync();

        var cashOutResult = await betGrain.CashOutAsync();

        cashOutResult.IsSuccess.Should().BeTrue();
        cashOutResult.Bet!.Status.Should().Be(BetStatus.CashOut);
        cashOutResult.Bet.Payout.Should().NotBeNull();
        cashOutResult.Bet.Payout!.Value.Amount.Should().BeGreaterThan(0);
        cashOutResult.Bet.Payout.Value.Amount.Should().BeLessThan(100m);

        var balanceAfterCashOut = await walletGrain.GetBalanceAsync();
        balanceAfterCashOut.Amount.Should().BeGreaterThan(balanceBeforeCashOut.Amount);

        var betManagerGrain = _cluster.GrainFactory.GetGrain<IBetManagerGrain>(userId);
        var activeBets = await betManagerGrain.GetActiveBetsAsync();
        activeBets.Should().BeEmpty();
    }

    [Fact]
    public async Task EventSourcingIntegration_ShouldPersistAndRestore()
    {
        var userId = "integration_user_005";
        var betId = Guid.NewGuid();

        var events = BettingTestHelpers.CreateBetEventSequence(betId, userId, BetStatus.Won);
        _eventStore.GetEventStreamAsync(betId.ToString())
            .Returns(new EventStream(betId.ToString(), events, events.Count, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        _eventStore.GetEventsAsync(betId.ToString())
            .Returns(events);

        var betGrain = _cluster.GrainFactory.GetGrain<IBetGrain>(betId);

        var betHistory = await betGrain.GetBetHistoryAsync();

        betHistory.Should().HaveCount(3);
        betHistory[0].Status.Should().Be(BetStatus.Pending);
        betHistory[1].Status.Should().Be(BetStatus.Accepted);
        betHistory[2].Status.Should().Be(BetStatus.Won);

        var finalBetDetails = await betGrain.GetBetDetailsAsync();
        finalBetDetails.Should().NotBeNull();
        finalBetDetails!.Status.Should().Be(BetStatus.Won);
        finalBetDetails.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task BetManagerPagination_ShouldWorkCorrectly()
    {
        var userId = "integration_user_006";
        var betManagerGrain = _cluster.GrainFactory.GetGrain<IBetManagerGrain>(userId);

        var betIds = new List<Guid>();
        for (int i = 0; i < 25; i++)
        {
            var betId = Guid.NewGuid();
            betIds.Add(betId);
            await betManagerGrain.AddBetAsync(betId);

            var betGrain = _cluster.GrainFactory.GetGrain<IBetGrain>(betId);
            var mockBet = BettingTestHelpers.CreateTestBet(
                betId, userId, amount: 10m + i, 
                placedAt: DateTimeOffset.UtcNow.AddMinutes(-i));
            
            betGrain.GetBetDetailsAsync().Returns(mockBet);
        }

        var firstPage = await betManagerGrain.GetUserBetsAsync(10);
        firstPage.Should().HaveCount(10);

        var allBets = await betManagerGrain.GetUserBetsAsync(50);
        allBets.Should().HaveCount(25);

        var history = await betManagerGrain.GetBetHistoryAsync(15);
        history.Should().HaveCount(15);
        history.Should().BeInDescendingOrder(bet => bet.PlacedAt);
    }

    [Fact]
    public async Task MultipleUsersWorkflow_ShouldIsolateCorrectly()
    {
        var user1 = "integration_user_007a";
        var user2 = "integration_user_007b";
        var eventId = Guid.NewGuid();
        var marketId = "multi_user_market";
        var selectionId = "multi_selection";

        await SetupEventAndOddsForTesting(eventId, marketId, selectionId);

        var wallet1 = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(user1);
        var wallet2 = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(user2);
        var betManager1 = _cluster.GrainFactory.GetGrain<IBetManagerGrain>(user1);
        var betManager2 = _cluster.GrainFactory.GetGrain<IBetManagerGrain>(user2);

        await wallet1.DepositAsync(Money.Create(1000m), "user1_deposit");
        await wallet2.DepositAsync(Money.Create(500m), "user2_deposit");

        var bet1Id = Guid.NewGuid();
        var bet2Id = Guid.NewGuid();
        var bet3Id = Guid.NewGuid();

        var bet1Grain = _cluster.GrainFactory.GetGrain<IBetGrain>(bet1Id);
        var bet2Grain = _cluster.GrainFactory.GetGrain<IBetGrain>(bet2Id);
        var bet3Grain = _cluster.GrainFactory.GetGrain<IBetGrain>(bet3Id);

        var user1Request1 = BettingTestHelpers.CreateValidPlaceBetRequest(
            bet1Id, user1, eventId, marketId, selectionId, amount: 200m);
        var user1Request2 = BettingTestHelpers.CreateValidPlaceBetRequest(
            bet2Id, user1, eventId, marketId, selectionId, amount: 150m);
        var user2Request = BettingTestHelpers.CreateValidPlaceBetRequest(
            bet3Id, user2, eventId, marketId, selectionId, amount: 100m);

        var result1 = await bet1Grain.PlaceBetAsync(user1Request1);
        var result2 = await bet2Grain.PlaceBetAsync(user1Request2);
        var result3 = await bet3Grain.PlaceBetAsync(user2Request);

        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result3.IsSuccess.Should().BeTrue();

        await betManager1.AddBetAsync(bet1Id);
        await betManager1.AddBetAsync(bet2Id);
        await betManager2.AddBetAsync(bet3Id);

        var user1Bets = await betManager1.GetUserBetsAsync();
        var user2Bets = await betManager2.GetUserBetsAsync();

        user1Bets.Should().HaveCount(2);
        user2Bets.Should().HaveCount(1);

        user1Bets.Should().OnlyContain(bet => bet.UserId == user1);
        user2Bets.Should().OnlyContain(bet => bet.UserId == user2);

        var user1Balance = await wallet1.GetBalanceAsync();
        var user2Balance = await wallet2.GetBalanceAsync();

        user1Balance.Amount.Should().Be(650m);
        user2Balance.Amount.Should().Be(400m);
    }

    private async Task SetupCompleteScenario(string userId, Guid eventId, string marketId, string selectionId, Guid betId)
    {
        await SetupEventAndOddsForTesting(eventId, marketId, selectionId);

        var walletGrain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(userId);
        await walletGrain.DepositAsync(Money.Create(500m), "initial_deposit");

        var betGrain = _cluster.GrainFactory.GetGrain<IBetGrain>(betId);
        var request = BettingTestHelpers.CreateValidPlaceBetRequest(
            betId, userId, eventId, marketId, selectionId, amount: 100m);

        var result = await betGrain.PlaceBetAsync(request);
        result.IsSuccess.Should().BeTrue();

        var betManagerGrain = _cluster.GrainFactory.GetGrain<IBetManagerGrain>(userId);
        await betManagerGrain.AddBetAsync(betId);
    }

    private async Task SetupEventAndOddsForTesting(Guid eventId, string marketId, string selectionId, decimal odds = 2.5m)
    {
        var eventGrain = _cluster.GrainFactory.GetGrain<ISportEventGrain>(eventId);
        var oddsGrain = _cluster.GrainFactory.GetGrain<IOddsGrain>(marketId);

        var sportEvent = new SportEvent(
            eventId,
            $"Test Event {eventId:N}",
            SportType.Football,
            "Test League",
            DateTimeOffset.UtcNow.AddHours(1),
            null,
            EventStatus.Scheduled,
            new Dictionary<string, string> { ["home"] = "Team A", ["away"] = "Team B" },
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        );
        await eventGrain.CreateAsync(sportEvent);

        var market = new Market(
            Guid.NewGuid(),
            eventId,
            "Test Market",
            "Test market description",
            MarketStatus.Open,
            new Dictionary<string, decimal> { [selectionId] = odds },
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        );
        await eventGrain.AddMarketAsync("Test Market", "Test market description", new Dictionary<string, decimal> { [selectionId] = odds });

        var oddsUpdate = new OddsUpdateRequest(
            marketId,
            new Dictionary<string, decimal> { [selectionId] = odds },
            OddsSource.Manual,
            "Initial odds",
            "test",
            DateTimeOffset.UtcNow
        );
        await oddsGrain.UpdateOddsAsync(oddsUpdate);
    }
}