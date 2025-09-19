using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Orleans.TestingHost;
using SportsbookLite.Contracts.Betting;
using SportsbookLite.Contracts.Events;
using SportsbookLite.Contracts.Odds;
using SportsbookLite.Contracts.Wallet;
using SportsbookLite.Grains.Betting;
using SportsbookLite.GrainInterfaces;
using SportsbookLite.Infrastructure.EventStore;
using SportsbookLite.TestUtilities;

namespace SportsbookLite.UnitTests.Features.Betting;

public class BetGrainTests : OrleansTestBase
{
    private TestCluster _cluster = null!;
    private IEventStore _eventStore = null!;
    private IUserWalletGrain _walletGrain = null!;
    private IOddsGrain _oddsGrain = null!;
    private IBetManagerGrain _betManagerGrain = null!;
    private static IEventStore? _sharedEventStore;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _eventStore = Substitute.For<IEventStore>();
        _sharedEventStore = _eventStore; // Share for SiloConfigurator
        _walletGrain = Substitute.For<IUserWalletGrain>();
        _oddsGrain = Substitute.For<IOddsGrain>();
        _betManagerGrain = Substitute.For<IBetManagerGrain>();

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
                if (_sharedEventStore != null)
                    services.AddSingleton(_sharedEventStore);
            });
        }
    }

    [Fact(Skip = "Requires refactoring: BetGrain uses GrainFactory internally, making it impossible to mock dependencies")]
    public async Task PlaceBetAsync_WithValidRequest_ShouldSucceed()
    {
        var betId = Guid.NewGuid();
        var request = CreateValidPlaceBetRequest(betId);
        var grain = CreateBetGrain(betId);

        await SetupSuccessfulBetPlacement(request);

        var result = await grain.PlaceBetAsync(request);

        result.IsSuccess.Should().BeTrue($"PlaceBetAsync failed with error: {result.Error}");
        result.Bet.Should().NotBeNull();
        result.Bet!.Id.Should().Be(betId);
        result.Bet.Status.Should().Be(BetStatus.Accepted);
        
        await _eventStore.Received().SaveEventsAsync(betId.ToString(), Arg.Any<IReadOnlyList<IDomainEvent>>());
        await _walletGrain.Received().CommitReservationAsync(betId.ToString());
        await _betManagerGrain.Received().AddBetAsync(betId);
    }

    [Fact]
    public async Task PlaceBetAsync_WithInvalidRequest_ShouldFail()
    {
        var betId = Guid.NewGuid();
        var invalidRequest = new PlaceBetRequest(
            betId, "", Guid.NewGuid(), "market", "selection",
            Money.Create(100m), 2.0m, BetType.Single);
        var grain = CreateBetGrain(betId);

        var result = await grain.PlaceBetAsync(invalidRequest);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid bet request");
    }

    [Fact]
    public async Task PlaceBetAsync_WithInsufficientBalance_ShouldFail()
    {
        var betId = Guid.NewGuid();
        var request = CreateValidPlaceBetRequest(betId);
        var grain = CreateBetGrain(betId);

        _walletGrain.GetAvailableBalanceAsync()
            .Returns(Money.Create(50m));
        _oddsGrain.GetCurrentOddsAsync()
            .Returns(CreateValidOddsSnapshot(request));

        var result = await grain.PlaceBetAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Insufficient balance");
    }

    [Fact(Skip = "Requires refactoring: BetGrain uses GrainFactory internally, making it impossible to mock dependencies")]
    public async Task PlaceBetAsync_WithSelectionNotFound_ShouldFail()
    {
        var betId = Guid.NewGuid();
        var request = CreateValidPlaceBetRequest(betId);
        var grain = CreateBetGrain(betId);

        _walletGrain.GetAvailableBalanceAsync()
            .Returns(Money.Create(200m));
        _oddsGrain.GetCurrentOddsAsync()
            .Returns(new OddsSnapshot(
                request.MarketId,
                new Dictionary<string, SportsbookLite.Contracts.Odds.Odds>(),
                DateTimeOffset.UtcNow,
                OddsVolatility.Low));

        var result = await grain.PlaceBetAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Selection not found");
    }

    [Fact(Skip = "Requires refactoring: BetGrain uses GrainFactory internally, making it impossible to mock dependencies")]
    public async Task PlaceBetAsync_WithOddsChanged_ShouldFail()
    {
        var betId = Guid.NewGuid();
        var request = CreateValidPlaceBetRequest(betId, acceptableOdds: 3.0m);
        var grain = CreateBetGrain(betId);

        _walletGrain.GetAvailableBalanceAsync()
            .Returns(Money.Create(200m));
        _oddsGrain.GetCurrentOddsAsync()
            .Returns(CreateValidOddsSnapshot(request, currentOdds: 2.5m));

        var result = await grain.PlaceBetAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Odds have changed and are no longer acceptable");
    }

    [Fact(Skip = "Requires refactoring: BetGrain uses GrainFactory internally, making it impossible to mock dependencies")]
    public async Task PlaceBetAsync_WithReservationFailure_ShouldFail()
    {
        var betId = Guid.NewGuid();
        var request = CreateValidPlaceBetRequest(betId);
        var grain = CreateBetGrain(betId);

        _walletGrain.GetAvailableBalanceAsync()
            .Returns(Money.Create(200m));
        _oddsGrain.GetCurrentOddsAsync()
            .Returns(CreateValidOddsSnapshot(request));
        _walletGrain.ReserveAsync(request.Amount, betId.ToString())
            .Returns(TransactionResult.Failure("Reservation failed"));

        var result = await grain.PlaceBetAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Failed to reserve funds: Reservation failed");
    }

    [Fact(Skip = "Requires refactoring: BetGrain uses GrainFactory internally, making it impossible to mock dependencies")]
    public async Task PlaceBetAsync_WithEventStoreFailure_ShouldReleaseReservation()
    {
        var betId = Guid.NewGuid();
        var request = CreateValidPlaceBetRequest(betId);
        var grain = CreateBetGrain(betId);

        SetupSuccessfulBetPlacement(request);
        _eventStore.SaveEventsAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<IDomainEvent>>())
            .Returns(ValueTask.FromException(new InvalidOperationException("Event store failure")));

        var result = await grain.PlaceBetAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to place bet");

        await _walletGrain.Received().ReleaseReservationAsync(betId.ToString());
        await _oddsGrain.Received().UnlockOddsAsync(betId.ToString());
    }

    [Fact]
    public async Task PlaceBetAsync_WhenBetAlreadyProcessed_ShouldFail()
    {
        var betId = Guid.NewGuid();
        var request = CreateValidPlaceBetRequest(betId);
        var grain = CreateBetGrainWithExistingBet(betId, BetStatus.Accepted);

        var result = await grain.PlaceBetAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Bet has already been processed");
    }

    [Fact]
    public async Task PlaceBetAsync_ShouldBeIdempotent()
    {
        var betId = Guid.NewGuid();
        var request = CreateValidPlaceBetRequest(betId);
        var grain = CreateBetGrainWithExistingBet(betId, BetStatus.Accepted);

        var firstResult = await grain.PlaceBetAsync(request);
        var secondResult = await grain.PlaceBetAsync(request);

        firstResult.IsSuccess.Should().BeFalse();
        secondResult.IsSuccess.Should().BeFalse();
        firstResult.Error.Should().Be(secondResult.Error);
    }

    [Fact]
    public async Task GetBetDetailsAsync_WithExistingBet_ShouldReturnBet()
    {
        var betId = Guid.NewGuid();
        var grain = CreateBetGrainWithExistingBet(betId, BetStatus.Accepted);

        var result = await grain.GetBetDetailsAsync();

        result.Should().NotBeNull();
        result!.Id.Should().Be(betId);
        result.Status.Should().Be(BetStatus.Accepted);
    }

    [Fact]
    public async Task GetBetDetailsAsync_WithNonExistentBet_ShouldReturnNull()
    {
        var betId = Guid.NewGuid();
        var grain = CreateBetGrain(betId);

        var result = await grain.GetBetDetailsAsync();

        result.Should().BeNull();
    }

    [Fact(Skip = "Requires refactoring: BetGrain uses GrainFactory internally, making it impossible to mock dependencies")]
    public async Task VoidBetAsync_WithValidBet_ShouldSucceed()
    {
        var betId = Guid.NewGuid();
        var reason = "Event cancelled";
        var grain = CreateBetGrainWithExistingBet(betId, BetStatus.Accepted);

        var result = await grain.VoidBetAsync(reason);

        result.IsSuccess.Should().BeTrue();
        result.Bet!.Status.Should().Be(BetStatus.Void);
        result.Bet.VoidReason.Should().Be(reason);
        
        await _eventStore.Received().SaveEventsAsync(betId.ToString(), Arg.Any<IReadOnlyList<IDomainEvent>>());
        await _walletGrain.Received().ReleaseReservationAsync(betId.ToString());
    }

    [Fact]
    public async Task VoidBetAsync_WithNonExistentBet_ShouldFail()
    {
        var betId = Guid.NewGuid();
        var grain = CreateBetGrain(betId);

        var result = await grain.VoidBetAsync("Test reason");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Bet not found");
    }

    [Fact]
    public async Task VoidBetAsync_WithNonVoidableBet_ShouldFail()
    {
        var betId = Guid.NewGuid();
        var grain = CreateBetGrainWithExistingBet(betId, BetStatus.Won);

        var result = await grain.VoidBetAsync("Test reason");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Bet cannot be voided in current status");
    }

    [Fact(Skip = "Requires refactoring: BetGrain uses GrainFactory internally, making it impossible to mock dependencies")]
    public async Task CashOutAsync_WithValidBet_ShouldSucceed()
    {
        var betId = Guid.NewGuid();
        var grain = CreateBetGrainWithExistingBet(betId, BetStatus.Accepted);
        var cashOutAmount = Money.Create(95m);

        var transaction = WalletTransaction.Create("user123", TransactionType.Deposit, cashOutAmount, $"cashout-{betId}");
        _walletGrain.DepositAsync(cashOutAmount, $"cashout-{betId}")
            .Returns(TransactionResult.Success(transaction, cashOutAmount));

        var result = await grain.CashOutAsync();

        result.IsSuccess.Should().BeTrue();
        result.Bet!.Status.Should().Be(BetStatus.CashOut);
        result.Bet.Payout!.Value.Amount.Should().BeApproximately(95m, 0.01m);
        
        await _eventStore.Received().SaveEventsAsync(betId.ToString(), Arg.Any<IReadOnlyList<IDomainEvent>>());
        await _walletGrain.Received().DepositAsync(Arg.Any<Money>(), $"cashout-{betId}");
    }

    [Fact]
    public async Task CashOutAsync_WithNonExistentBet_ShouldFail()
    {
        var betId = Guid.NewGuid();
        var grain = CreateBetGrain(betId);

        var result = await grain.CashOutAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Bet not found");
    }

    [Fact]
    public async Task CashOutAsync_WithNonCashOutableBet_ShouldFail()
    {
        var betId = Guid.NewGuid();
        var grain = CreateBetGrainWithExistingBet(betId, BetStatus.Won);

        var result = await grain.CashOutAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Bet cannot be cashed out");
    }

    [Fact(Skip = "Requires refactoring: BetGrain uses GrainFactory internally, making it impossible to mock dependencies")]
    public async Task CashOutAsync_WithDepositFailure_ShouldFail()
    {
        var betId = Guid.NewGuid();
        var grain = CreateBetGrainWithExistingBet(betId, BetStatus.Accepted);

        _walletGrain.DepositAsync(Arg.Any<Money>(), Arg.Any<string>())
            .Returns(TransactionResult.Failure("Deposit failed"));

        var result = await grain.CashOutAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Failed to process cash out");
    }

    [Fact]
    public async Task GetBetHistoryAsync_ShouldReturnEventSourcingHistory()
    {
        var betId = Guid.NewGuid();
        var events = CreateBetEventHistory(betId);
        var grain = CreateBetGrain(betId);

        _eventStore.GetEventsAsync(betId.ToString())
            .Returns(events);

        var history = await grain.GetBetHistoryAsync();

        history.Should().HaveCount(3);
        history[0].Status.Should().Be(BetStatus.Pending);
        history[1].Status.Should().Be(BetStatus.Accepted);
        history[2].Status.Should().Be(BetStatus.Won);
    }

    private IBetGrain CreateBetGrain(Guid betId)
    {
        _eventStore.GetEventStreamAsync(betId.ToString())
            .Returns((EventStream?)null);
        
        return _cluster.GrainFactory.GetGrain<IBetGrain>(betId);
    }

    private IBetGrain CreateBetGrainWithExistingBet(Guid betId, BetStatus status)
    {
        var events = CreateBetEvents(betId, status);
        var eventStream = new EventStream(betId.ToString(), events, events.Count, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        
        _eventStore.GetEventStreamAsync(betId.ToString())
            .Returns(eventStream);
        
        return _cluster.GrainFactory.GetGrain<IBetGrain>(betId);
    }

    private async Task SetupSuccessfulBetPlacement(PlaceBetRequest request)
    {
        // Since BetGrain uses GrainFactory to get other grains, we need to set up actual grains in the cluster
        var walletGrain = _cluster.GrainFactory.GetGrain<IUserWalletGrain>(request.UserId);
        var oddsGrain = _cluster.GrainFactory.GetGrain<IOddsGrain>(request.MarketId);
        var betManagerGrain = _cluster.GrainFactory.GetGrain<IBetManagerGrain>(request.UserId);
        
        // Initialize wallet with balance
        await walletGrain.DepositAsync(Money.Create(500m), "test-deposit");
        
        // Initialize odds
        await oddsGrain.InitializeMarketAsync(
            new Dictionary<string, decimal> 
            { 
                [request.SelectionId] = request.AcceptableOdds + 0.5m 
            },
            OddsSource.Manual
        );
        
        // Mock the event store to succeed
        _eventStore.SaveEventsAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<IDomainEvent>>())
            .Returns(ValueTask.CompletedTask);
    }

    private static PlaceBetRequest CreateValidPlaceBetRequest(Guid betId, decimal acceptableOdds = 2.0m)
    {
        return new PlaceBetRequest(
            betId,
            "user123",
            Guid.NewGuid(),
            "market123",
            "selection456",
            Money.Create(100m),
            acceptableOdds,
            BetType.Single
        );
    }

    private static OddsSnapshot CreateValidOddsSnapshot(PlaceBetRequest request, decimal currentOdds = 2.5m)
    {
        var selections = new Dictionary<string, SportsbookLite.Contracts.Odds.Odds>
        {
            [request.SelectionId] = new SportsbookLite.Contracts.Odds.Odds(
                currentOdds,
                request.MarketId,
                request.SelectionId,
                OddsSource.Provider,
                DateTimeOffset.UtcNow
            )
        };

        return new OddsSnapshot(
            request.MarketId,
            selections,
            DateTimeOffset.UtcNow,
            OddsVolatility.Low
        );
    }

    private static List<IDomainEvent> CreateBetEvents(Guid betId, BetStatus finalStatus)
    {
        var events = new List<IDomainEvent>
        {
            new BetPlacedEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                betId.ToString(),
                betId,
                "user123",
                Guid.NewGuid(),
                "market123",
                "selection456",
                Money.Create(100m),
                2.0m,
                BetType.Single
            )
        };

        if (finalStatus != BetStatus.Pending)
        {
            events.Add(new BetAcceptedEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                betId.ToString(),
                betId,
                "user123",
                2.0m,
                Money.Create(200m)
            ));

            if (finalStatus == BetStatus.Won || finalStatus == BetStatus.Lost)
            {
                events.Add(new BetSettledEvent(
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    betId.ToString(),
                    betId,
                    "user123",
                    finalStatus,
                    finalStatus == BetStatus.Won ? Money.Create(200m) : null
                ));
            }
            else if (finalStatus == BetStatus.Void)
            {
                events.Add(new BetVoidedEvent(
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    betId.ToString(),
                    betId,
                    "user123",
                    "Test void"
                ));
            }
        }

        return events;
    }

    private static List<IDomainEvent> CreateBetEventHistory(Guid betId)
    {
        return new List<IDomainEvent>
        {
            new BetPlacedEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddMinutes(-10),
                betId.ToString(),
                betId,
                "user123",
                Guid.NewGuid(),
                "market123",
                "selection456",
                Money.Create(100m),
                2.0m,
                BetType.Single
            ),
            new BetAcceptedEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddMinutes(-9),
                betId.ToString(),
                betId,
                "user123",
                2.0m,
                Money.Create(200m)
            ),
            new BetSettledEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddMinutes(-5),
                betId.ToString(),
                betId,
                "user123",
                BetStatus.Won,
                Money.Create(200m)
            )
        };
    }
}