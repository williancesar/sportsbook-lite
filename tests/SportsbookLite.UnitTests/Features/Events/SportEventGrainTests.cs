using FluentAssertions;
using SportsbookLite.Contracts.Events;
using SportsbookLite.Grains.Events;
using SportsbookLite.GrainInterfaces;
using SportsbookLite.TestUtilities;
using Orleans.TestingHost;

namespace SportsbookLite.UnitTests.Features.Events;

public class SportEventGrainTests : OrleansTestBase
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
    public async Task CreateEventAsync_WithValidData_ShouldSucceed()
    {
        var eventId = Guid.NewGuid();
        var name = "Liverpool vs Arsenal";
        var sportType = SportType.Soccer;
        var competition = "Premier League";
        var startTime = DateTimeOffset.UtcNow.AddDays(1);
        var participants = new Dictionary<string, string>
        {
            { "home", "Liverpool" },
            { "away", "Arsenal" }
        };

        var grain = _cluster.GrainFactory.GetGrain<ISportEventGrain>(eventId);

        var result = await grain.CreateEventAsync(name, sportType, competition, startTime, participants);

        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be(name);
        result.SportType.Should().Be(sportType);
        result.Competition.Should().Be(competition);
        result.StartTime.Should().Be(startTime);
        result.Status.Should().Be(EventStatus.Scheduled);
        result.Participants.Should().BeEquivalentTo(participants);
        result.EndTime.Should().BeNull();
        result.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CreateEventAsync_WhenEventAlreadyExists_ShouldThrowException()
    {
        var eventId = Guid.NewGuid();
        var name = "Test Event";
        var participants = new Dictionary<string, string> { { "home", "Team A" }, { "away", "Team B" } };
        var grain = _cluster.GrainFactory.GetGrain<ISportEventGrain>(eventId);

        await grain.CreateEventAsync(name, SportType.Soccer, "Test League", DateTimeOffset.UtcNow.AddDays(1), participants);

        var act = async () => await grain.CreateEventAsync(name, SportType.Soccer, "Test League", DateTimeOffset.UtcNow.AddDays(1), participants);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Event already exists");
    }

    [Fact]
    public async Task GetEventDetailsAsync_WhenEventDoesNotExist_ShouldThrowException()
    {
        var eventId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ISportEventGrain>(eventId);

        var act = async () => await grain.GetEventDetailsAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Event does not exist");
    }

    [Fact]
    public async Task UpdateEventAsync_WithValidData_ShouldSucceed()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        var newName = "Updated Event Name";
        var newStartTime = DateTimeOffset.UtcNow.AddDays(2);
        var newParticipants = new Dictionary<string, string> { { "home", "New Team A" }, { "away", "New Team B" } };

        var result = await grain.UpdateEventAsync(newName, newStartTime, newParticipants);

        result.Name.Should().Be(newName);
        result.StartTime.Should().Be(newStartTime);
        result.Participants.Should().BeEquivalentTo(newParticipants);
        result.Status.Should().Be(EventStatus.Scheduled);
        result.LastModified.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateEventAsync_WhenEventNotScheduled_ShouldThrowException()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        await grain.StartEventAsync();

        var act = async () => await grain.UpdateEventAsync("New Name");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot update event in status Live");
    }

    [Fact]
    public async Task StartEventAsync_FromScheduled_ShouldSucceed()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);

        var result = await grain.StartEventAsync();

        result.Status.Should().Be(EventStatus.Live);
        result.LastModified.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(EventStatus.Completed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task StartEventAsync_FromInvalidStatus_ShouldThrowException(EventStatus invalidFromStatus)
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        
        if (invalidFromStatus == EventStatus.Completed)
        {
            await grain.StartEventAsync();
            await grain.CompleteEventAsync();
        }
        else if (invalidFromStatus == EventStatus.Cancelled)
        {
            await grain.CancelEventAsync("Test cancellation");
        }

        var act = async () => await grain.StartEventAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Cannot start event from status {invalidFromStatus}");
    }

    [Fact]
    public async Task CompleteEventAsync_FromLive_ShouldSucceed()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        await grain.StartEventAsync();

        var result = await grain.CompleteEventAsync();

        result.Status.Should().Be(EventStatus.Completed);
        result.EndTime.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        result.LastModified.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CompleteEventAsync_FromLiveWithResult_ShouldSucceed()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        await grain.StartEventAsync();
        var eventResult = EventResult.Create(eventId, new Dictionary<string, object> { { "score", "2-1" } });

        var result = await grain.CompleteEventAsync(eventResult);

        result.Status.Should().Be(EventStatus.Completed);
        result.EndTime.Should().NotBeNull();

        var storedResult = await grain.GetResultAsync();
        storedResult.Should().NotBeNull();
        storedResult!.Value.EventId.Should().Be(eventId);
        storedResult.Value.Results["score"].Should().Be("2-1");
    }

    [Theory]
    [InlineData(EventStatus.Scheduled)]
    [InlineData(EventStatus.Completed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task CompleteEventAsync_FromInvalidStatus_ShouldThrowException(EventStatus invalidFromStatus)
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);

        if (invalidFromStatus == EventStatus.Completed)
        {
            await grain.StartEventAsync();
            await grain.CompleteEventAsync();
        }
        else if (invalidFromStatus == EventStatus.Cancelled)
        {
            await grain.CancelEventAsync("Test cancellation");
        }

        var act = async () => await grain.CompleteEventAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Cannot complete event from status {invalidFromStatus}");
    }

    [Fact]
    public async Task CancelEventAsync_FromScheduled_ShouldSucceed()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        var cancellationReason = "Weather conditions";

        var result = await grain.CancelEventAsync(cancellationReason);

        result.Status.Should().Be(EventStatus.Cancelled);
        result.LastModified.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(EventStatus.Live)]
    [InlineData(EventStatus.Completed)]
    public async Task CancelEventAsync_FromInvalidStatus_ShouldThrowException(EventStatus invalidFromStatus)
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);

        if (invalidFromStatus == EventStatus.Live)
        {
            await grain.StartEventAsync();
        }
        else if (invalidFromStatus == EventStatus.Completed)
        {
            await grain.StartEventAsync();
            await grain.CompleteEventAsync();
        }

        var act = async () => await grain.CancelEventAsync("Test cancellation");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Cannot cancel event from status {invalidFromStatus}");
    }

    [Fact]
    public async Task AddMarketAsync_WithValidData_ShouldSucceed()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        var eventDetails = await grain.GetEventDetailsAsync();
        var marketName = "Match Winner";
        var description = "Select the winner of the match";
        var outcomes = new Dictionary<string, decimal>
        {
            { "home", 1.85m },
            { "away", 2.10m },
            { "draw", 3.40m }
        };

        var result = await grain.AddMarketAsync(marketName, description, outcomes);

        result.Id.Should().NotBeEmpty();
        result.EventId.Should().Be(eventDetails.Id);
        result.Name.Should().Be(marketName);
        result.Description.Should().Be(description);
        result.Status.Should().Be(MarketStatus.Open);
        result.Outcomes.Should().BeEquivalentTo(outcomes);
        result.WinningOutcome.Should().BeNull();
    }

    [Fact]
    public async Task AddMarketAsync_WhenEventDoesNotExist_ShouldThrowException()
    {
        var eventId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ISportEventGrain>(eventId);
        var outcomes = new Dictionary<string, decimal> { { "home", 1.85m } };

        var act = async () => await grain.AddMarketAsync("Test Market", "Description", outcomes);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Event does not exist");
    }

    [Fact]
    public async Task GetMarketsAsync_ShouldReturnAllMarkets()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        var eventDetails = await grain.GetEventDetailsAsync();
        var outcomes = new Dictionary<string, decimal> { { "home", 1.85m }, { "away", 2.10m } };

        await grain.AddMarketAsync("Market 1", "Description 1", outcomes);
        await grain.AddMarketAsync("Market 2", "Description 2", outcomes);

        var markets = await grain.GetMarketsAsync();

        markets.Should().HaveCount(2);
        markets.Select(m => m.Name).Should().Contain(new[] { "Market 1", "Market 2" });
        markets.All(m => m.EventId == eventDetails.Id).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateMarketStatusAsync_WithValidTransition_ShouldSucceed()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        var outcomes = new Dictionary<string, decimal> { { "home", 1.85m } };
        var market = await grain.AddMarketAsync("Test Market", "Description", outcomes);

        var result = await grain.UpdateMarketStatusAsync(market.Id, MarketStatus.Suspended);

        result.Status.Should().Be(MarketStatus.Suspended);
        result.Id.Should().Be(market.Id);
        result.LastModified.Should().BeAfter(market.LastModified);
    }

    [Fact]
    public async Task UpdateMarketStatusAsync_WithInvalidTransition_ShouldThrowException()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        var outcomes = new Dictionary<string, decimal> { { "home", 1.85m } };
        var market = await grain.AddMarketAsync("Test Market", "Description", outcomes);
        await grain.UpdateMarketStatusAsync(market.Id, MarketStatus.Closed);

        var act = async () => await grain.UpdateMarketStatusAsync(market.Id, MarketStatus.Open);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot transition market from Closed to Open");
    }

    [Fact]
    public async Task UpdateMarketStatusAsync_WithNonExistentMarket_ShouldThrowException()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        var nonExistentMarketId = Guid.NewGuid();

        var act = async () => await grain.UpdateMarketStatusAsync(nonExistentMarketId, MarketStatus.Suspended);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Market does not exist");
    }

    [Fact]
    public async Task SetMarketResultAsync_WithValidData_ShouldSucceed()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        var outcomes = new Dictionary<string, decimal> { { "home", 1.85m }, { "away", 2.10m } };
        var market = await grain.AddMarketAsync("Test Market", "Description", outcomes);
        await grain.UpdateMarketStatusAsync(market.Id, MarketStatus.Closed);

        var result = await grain.SetMarketResultAsync(market.Id, "home");

        result.Status.Should().Be(MarketStatus.Settled);
        result.WinningOutcome.Should().Be("home");
        result.Id.Should().Be(market.Id);
    }

    [Fact]
    public async Task SetMarketResultAsync_WithInvalidOutcome_ShouldThrowException()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        var outcomes = new Dictionary<string, decimal> { { "home", 1.85m } };
        var market = await grain.AddMarketAsync("Test Market", "Description", outcomes);
        await grain.UpdateMarketStatusAsync(market.Id, MarketStatus.Closed);

        var act = async () => await grain.SetMarketResultAsync(market.Id, "invalid_outcome");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Invalid winning outcome: invalid_outcome");
    }

    [Fact]
    public async Task SetMarketResultAsync_WithMarketNotClosed_ShouldThrowException()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        var outcomes = new Dictionary<string, decimal> { { "home", 1.85m } };
        var market = await grain.AddMarketAsync("Test Market", "Description", outcomes);

        var act = async () => await grain.SetMarketResultAsync(market.Id, "home");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Market must be closed before settling. Current status: Open");
    }

    [Fact]
    public async Task CompleteEventAsync_ShouldSuspendAllOpenMarkets()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        var outcomes = new Dictionary<string, decimal> { { "home", 1.85m } };
        
        await grain.AddMarketAsync("Market 1", "Description 1", outcomes);
        await grain.AddMarketAsync("Market 2", "Description 2", outcomes);
        await grain.StartEventAsync();

        await grain.CompleteEventAsync();

        var markets = await grain.GetMarketsAsync();
        markets.All(m => m.Status == MarketStatus.Suspended).Should().BeTrue();
    }

    [Fact]
    public async Task CancelEventAsync_ShouldSuspendAllOpenMarkets()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        var outcomes = new Dictionary<string, decimal> { { "home", 1.85m } };
        
        await grain.AddMarketAsync("Market 1", "Description 1", outcomes);
        await grain.AddMarketAsync("Market 2", "Description 2", outcomes);

        await grain.CancelEventAsync("Test cancellation");

        var markets = await grain.GetMarketsAsync();
        markets.All(m => m.Status == MarketStatus.Suspended).Should().BeTrue();
    }

    [Fact]
    public async Task SetResultAsync_WithValidData_ShouldSucceed()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        var results = new Dictionary<string, object>
        {
            { "home_score", 2 },
            { "away_score", 1 },
            { "winner", "home" }
        };

        var result = await grain.SetResultAsync(results, true);

        var eventDetails = await grain.GetEventDetailsAsync();
        result.EventId.Should().Be(eventDetails.Id);
        result.Results.Should().BeEquivalentTo(results);
        result.IsOfficial.Should().BeTrue();
        result.ResultTime.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        var storedResult = await grain.GetResultAsync();
        storedResult.Should().NotBeNull();
        storedResult!.Value.EventId.Should().Be(result.EventId);
        storedResult.Value.IsOfficial.Should().Be(result.IsOfficial);
        storedResult.Value.Results.Should().BeEquivalentTo(result.Results);
    }

    [Fact]
    public async Task GetResultAsync_WhenNoResult_ShouldReturnNull()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);

        var result = await grain.GetResultAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldMaintainConsistency()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);
        var outcomes = new Dictionary<string, decimal> { { "home", 1.85m } };

        var tasks = new List<Task<Market>>();
        for (int i = 0; i < 5; i++)
        {
            var marketName = $"Market {i}";
            tasks.Add(grain.AddMarketAsync(marketName, "Description", outcomes).AsTask());
        }

        var markets = await Task.WhenAll(tasks);

        markets.Should().HaveCount(5);
        markets.Select(m => m.Id).Should().OnlyHaveUniqueItems();
        
        var allMarkets = await grain.GetMarketsAsync();
        allMarkets.Should().HaveCount(5);
    }

    [Fact]
    public async Task EventLifecycleTransitions_ShouldWorkCorrectly()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);

        var scheduledEvent = await grain.GetEventDetailsAsync();
        scheduledEvent.Status.Should().Be(EventStatus.Scheduled);

        var liveEvent = await grain.StartEventAsync();
        liveEvent.Status.Should().Be(EventStatus.Live);

        var completedEvent = await grain.CompleteEventAsync();
        completedEvent.Status.Should().Be(EventStatus.Completed);
        completedEvent.EndTime.Should().NotBeNull();

        var finalEvent = await grain.GetEventDetailsAsync();
        finalEvent.Status.Should().Be(EventStatus.Completed);
        finalEvent.EndTime.Should().Be(completedEvent.EndTime);
    }

    private async Task<ISportEventGrain> CreateTestEventAsync(Guid eventId, string name = "Test Event")
    {
        var grain = _cluster.GrainFactory.GetGrain<ISportEventGrain>(eventId);
        var participants = new Dictionary<string, string> { { "home", "Team A" }, { "away", "Team B" } };
        
        await grain.CreateEventAsync(
            name,
            SportType.Soccer,
            "Test League",
            DateTimeOffset.UtcNow.AddDays(1),
            participants);
            
        return grain;
    }
}