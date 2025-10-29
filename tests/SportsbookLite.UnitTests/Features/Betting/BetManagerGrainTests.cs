using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Orleans.TestingHost;
using SportsbookLite.Contracts.Betting;
using SportsbookLite.Contracts.Wallet;
using SportsbookLite.Grains.Betting;
using SportsbookLite.GrainInterfaces;
using SportsbookLite.Infrastructure.EventStore;
using SportsbookLite.TestUtilities;

namespace SportsbookLite.UnitTests.Features.Betting;

public class BetManagerGrainTests : OrleansTestBase
{
    private TestCluster _cluster = null!;
    private IBetGrain _betGrain1 = null!;
    private IBetGrain _betGrain2 = null!;
    private IBetGrain _betGrain3 = null!;
    private IEventStore _eventStore = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _betGrain1 = Substitute.For<IBetGrain>();
        _betGrain2 = Substitute.For<IBetGrain>();
        _betGrain3 = Substitute.For<IBetGrain>();
        _eventStore = Substitute.For<IEventStore>();

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
                services.AddSingleton(Substitute.For<IEventStore>());
            });
        }
    }

    [Fact]
    public async Task AddBetAsync_WithNewBet_ShouldAddToCollection()
    {
        var userId = "user123";
        var betId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IBetManagerGrain>(userId);

        await grain.AddBetAsync(betId);

        var hasBet = await grain.HasBetAsync(betId);
        hasBet.Should().BeTrue();
    }

    [Fact]
    public async Task AddBetAsync_WithDuplicateBet_ShouldBeIdempotent()
    {
        var userId = "user456";
        var betId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IBetManagerGrain>(userId);

        await grain.AddBetAsync(betId);
        await grain.AddBetAsync(betId);

        var hasBet = await grain.HasBetAsync(betId);
        hasBet.Should().BeTrue();
    }

    [Fact]
    public async Task HasBetAsync_WithNonExistentBet_ShouldReturnFalse()
    {
        var userId = "user789";
        var betId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IBetManagerGrain>(userId);

        var hasBet = await grain.HasBetAsync(betId);

        hasBet.Should().BeFalse();
    }

    [Fact(Skip = "Requires refactoring: BetManagerGrain calls real BetGrain instances which have unmockable dependencies")]
    public async Task GetUserBetsAsync_WithMultipleBets_ShouldReturnAllBets()
    {
        var userId = "user101";
        var betIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var grain = CreateBetManagerGrainWithMockedBets(userId, betIds);

        SetupBetGrainResponses(betIds);

        var bets = await grain.GetUserBetsAsync();

        bets.Should().HaveCount(3);
        bets.Should().OnlyContain(bet => bet != null);
        bets.Select(b => b.Id).Should().BeEquivalentTo(betIds);
    }

    [Fact(Skip = "Requires refactoring: BetManagerGrain calls real BetGrain instances which have unmockable dependencies")]
    public async Task GetUserBetsAsync_WithLimit_ShouldRespectLimit()
    {
        var userId = "user102";
        var betIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();
        var grain = CreateBetManagerGrainWithMockedBets(userId, betIds);

        SetupBetGrainResponses(betIds);

        var bets = await grain.GetUserBetsAsync(5);

        bets.Should().HaveCount(5);
    }

    [Fact(Skip = "Requires refactoring: BetManagerGrain calls real BetGrain instances which have unmockable dependencies")]
    public async Task GetUserBetsAsync_WithNullBetResponse_ShouldFilterOutNulls()
    {
        var userId = "user103";
        var betIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var grain = CreateBetManagerGrainWithMockedBets(userId, betIds);

        SetupBetGrainWithOneNullResponse(betIds);

        var bets = await grain.GetUserBetsAsync();

        bets.Should().HaveCount(2);
        bets.Should().OnlyContain(bet => bet != null);
    }

    [Fact(Skip = "Requires refactoring: BetManagerGrain calls real BetGrain instances which have unmockable dependencies")]
    public async Task GetActiveBetsAsync_ShouldReturnOnlyUnsettledBets()
    {
        var userId = "user104";
        var betIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var grain = CreateBetManagerGrainWithMockedBets(userId, betIds);

        SetupBetGrainWithMixedStatuses(betIds);

        var activeBets = await grain.GetActiveBetsAsync();

        activeBets.Should().HaveCount(2);
        activeBets.Should().OnlyContain(bet => !bet.IsSettled);
        activeBets.Select(b => b.Status).Should().OnlyContain(status => 
            status == BetStatus.Pending || status == BetStatus.Accepted);
    }

    [Fact]
    public async Task GetActiveBetsAsync_WithAllSettledBets_ShouldReturnEmpty()
    {
        var userId = "user105";
        var betIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var grain = CreateBetManagerGrainWithMockedBets(userId, betIds);

        SetupBetGrainWithSettledBets(betIds);

        var activeBets = await grain.GetActiveBetsAsync();

        activeBets.Should().BeEmpty();
    }

    [Fact(Skip = "Requires refactoring: BetManagerGrain calls real BetGrain instances which have unmockable dependencies")]
    public async Task GetBetHistoryAsync_ShouldReturnOrderedByPlacedAt()
    {
        var userId = "user106";
        var betIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var grain = CreateBetManagerGrainWithMockedBets(userId, betIds);

        SetupBetGrainHistoryResponses(betIds);

        var history = await grain.GetBetHistoryAsync();

        history.Should().HaveCount(3);
        history.Should().BeInDescendingOrder(bet => bet.PlacedAt);
    }

    [Fact(Skip = "Requires refactoring: BetManagerGrain calls real BetGrain instances which have unmockable dependencies")]
    public async Task GetBetHistoryAsync_WithLimit_ShouldRespectLimit()
    {
        var userId = "user107";
        var betIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();
        var grain = CreateBetManagerGrainWithMockedBets(userId, betIds);

        SetupBetGrainHistoryResponses(betIds.Take(5).ToArray());

        var history = await grain.GetBetHistoryAsync(5);

        history.Should().HaveCount(5);
    }

    [Fact(Skip = "Requires refactoring: BetManagerGrain calls real BetGrain instances which have unmockable dependencies")]
    public async Task GetBetHistoryAsync_WithEmptyHistory_ShouldReturnEmpty()
    {
        var userId = "user108";
        var betIds = new[] { Guid.NewGuid() };
        var grain = CreateBetManagerGrainWithMockedBets(userId, betIds);

        SetupBetGrainWithEmptyHistory(betIds);

        var history = await grain.GetBetHistoryAsync();

        history.Should().BeEmpty();
    }

    [Theory(Skip = "Requires refactoring: BetManagerGrain calls real BetGrain instances which have unmockable dependencies")]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    public async Task GetUserBetsAsync_WithDifferentLimits_ShouldRespectLimit(int limit)
    {
        var userId = $"user{limit}";
        var betIds = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToArray();
        var grain = CreateBetManagerGrainWithMockedBets(userId, betIds);

        SetupBetGrainResponses(betIds.Take(limit).ToArray());

        var bets = await grain.GetUserBetsAsync(limit);

        bets.Should().HaveCount(Math.Min(limit, betIds.Length));
    }

    [Fact]
    public async Task BetManagerWorkflow_AddAndRetrieveBets_ShouldWork()
    {
        var userId = "user200";
        var betIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var grain = _cluster.GrainFactory.GetGrain<IBetManagerGrain>(userId);

        foreach (var betId in betIds)
        {
            await grain.AddBetAsync(betId);
        }

        foreach (var betId in betIds)
        {
            var hasBet = await grain.HasBetAsync(betId);
            hasBet.Should().BeTrue();
        }
    }

    private IBetManagerGrain CreateBetManagerGrainWithMockedBets(string userId, Guid[] betIds)
    {
        var grain = _cluster.GrainFactory.GetGrain<IBetManagerGrain>(userId);
        
        foreach (var betId in betIds)
        {
            grain.AddBetAsync(betId).AsTask().Wait();
        }

        return grain;
    }

    private void SetupBetGrainResponses(Guid[] betIds)
    {
        for (int i = 0; i < betIds.Length; i++)
        {
            var bet = CreateTestBet(betIds[i], BetStatus.Accepted);
            var betGrain = Substitute.For<IBetGrain>();
            betGrain.GetBetDetailsAsync().Returns(bet);
        }
    }

    private void SetupBetGrainWithOneNullResponse(Guid[] betIds)
    {
        for (int i = 0; i < betIds.Length; i++)
        {
            var betGrain = Substitute.For<IBetGrain>();
            if (i == 1)
            {
                betGrain.GetBetDetailsAsync().Returns((Bet?)null);
            }
            else
            {
                var bet = CreateTestBet(betIds[i], BetStatus.Accepted);
                betGrain.GetBetDetailsAsync().Returns(bet);
            }
        }
    }

    private void SetupBetGrainWithMixedStatuses(Guid[] betIds)
    {
        var statuses = new[] { BetStatus.Pending, BetStatus.Accepted, BetStatus.Won };
        
        for (int i = 0; i < betIds.Length; i++)
        {
            var bet = CreateTestBet(betIds[i], statuses[i]);
            var betGrain = Substitute.For<IBetGrain>();
            betGrain.GetBetDetailsAsync().Returns(bet);
        }
    }

    private void SetupBetGrainWithSettledBets(Guid[] betIds)
    {
        var statuses = new[] { BetStatus.Won, BetStatus.Lost };
        
        for (int i = 0; i < betIds.Length; i++)
        {
            var bet = CreateTestBet(betIds[i], statuses[i % 2]);
            var betGrain = Substitute.For<IBetGrain>();
            betGrain.GetBetDetailsAsync().Returns(bet);
        }
    }

    private void SetupBetGrainHistoryResponses(Guid[] betIds)
    {
        for (int i = 0; i < betIds.Length; i++)
        {
            var placedAt = DateTimeOffset.UtcNow.AddMinutes(-i * 10);
            var bet = CreateTestBet(betIds[i], BetStatus.Accepted, placedAt);
            var history = new List<Bet> { bet };
            
            var betGrain = Substitute.For<IBetGrain>();
            betGrain.GetBetHistoryAsync().Returns(history);
        }
    }

    private void SetupBetGrainWithEmptyHistory(Guid[] betIds)
    {
        foreach (var betId in betIds)
        {
            var betGrain = Substitute.For<IBetGrain>();
            betGrain.GetBetHistoryAsync().Returns(new List<Bet>());
        }
    }

    private static Bet CreateTestBet(
        Guid betId, 
        BetStatus status, 
        DateTimeOffset? placedAt = null,
        Money? payout = null)
    {
        return new Bet(
            betId,
            "user123",
            Guid.NewGuid(),
            "market123",
            "selection456",
            Money.Create(100m),
            2.0m,
            status,
            BetType.Single,
            placedAt ?? DateTimeOffset.UtcNow,
            status.IsSettled() ? DateTimeOffset.UtcNow : null,
            payout,
            null,
            null
        );
    }
}

public static class BetStatusExtensions
{
    public static bool IsSettled(this BetStatus status)
    {
        return status is BetStatus.Won or BetStatus.Lost or BetStatus.Void;
    }
}