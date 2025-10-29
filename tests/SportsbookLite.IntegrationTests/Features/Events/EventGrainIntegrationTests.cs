using FluentAssertions;
using Orleans;
using Orleans.TestingHost;
using SportsbookLite.Contracts.Events;
using SportsbookLite.GrainInterfaces;
using SportsbookLite.Grains.Events;
using SportsbookLite.TestUtilities;

namespace SportsbookLite.IntegrationTests.Features.Events;

public class EventGrainIntegrationTests : BaseIntegrationTest
{
    private TestCluster _cluster = null!;

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
    public async Task EventGrain_StatePersistence_ShouldMaintainStateAcrossActivations()
    {
        var eventId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ISportEventGrain>(eventId);
        var participants = new Dictionary<string, string> { { "home", "Team A" }, { "away", "Team B" } };

        await grain.CreateEventAsync("Persistence Test", SportType.Soccer, "Test League", DateTimeOffset.UtcNow.AddDays(1), participants);
        await grain.StartEventAsync();
        var marketResult = await grain.AddMarketAsync("Test Market", "Description", new Dictionary<string, decimal> { { "home", 1.85m } });

        _cluster.GrainFactory.GetGrain<ISportEventGrain>(eventId);

        var eventDetails = await grain.GetEventDetailsAsync();
        var markets = await grain.GetMarketsAsync();

        eventDetails.Status.Should().Be(EventStatus.Live);
        eventDetails.Name.Should().Be("Persistence Test");
        markets.Should().HaveCount(1);
        markets.First().Name.Should().Be("Test Market");
    }

    [Fact]
    public async Task EventGrain_ComplexEventWorkflow_ShouldHandleFullLifecycle()
    {
        var eventId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ISportEventGrain>(eventId);
        var participants = new Dictionary<string, string> 
        { 
            { "home", "Liverpool" }, 
            { "away", "Arsenal" } 
        };

        var createdEvent = await grain.CreateEventAsync(
            "Liverpool vs Arsenal",
            SportType.Soccer,
            "Premier League",
            DateTimeOffset.UtcNow.AddDays(1),
            participants);

        createdEvent.Status.Should().Be(EventStatus.Scheduled);

        var matchWinnerMarket = await grain.AddMarketAsync(
            "Match Winner",
            "Select the winner of the match",
            new Dictionary<string, decimal>
            {
                { "liverpool", 1.85m },
                { "arsenal", 2.10m },
                { "draw", 3.40m }
            });

        var totalGoalsMarket = await grain.AddMarketAsync(
            "Total Goals",
            "Total goals in the match",
            new Dictionary<string, decimal>
            {
                { "under_2.5", 2.20m },
                { "over_2.5", 1.65m }
            });

        var startedEvent = await grain.StartEventAsync();
        startedEvent.Status.Should().Be(EventStatus.Live);

        var eventResult = EventResult.Create(
            eventId,
            new Dictionary<string, object>
            {
                { "home_score", 2 },
                { "away_score", 1 },
                { "winner", "liverpool" },
                { "total_goals", 3 }
            });

        var completedEvent = await grain.CompleteEventAsync(eventResult);
        completedEvent.Status.Should().Be(EventStatus.Completed);
        completedEvent.EndTime.Should().NotBeNull();

        var storedResult = await grain.GetResultAsync();
        storedResult.Should().NotBeNull();
        storedResult!.Value.Results["winner"].Should().Be("liverpool");
        storedResult.Value.Results["total_goals"].Should().Be(3);

        var finalMarkets = await grain.GetMarketsAsync();
        finalMarkets.All(m => m.Status == MarketStatus.Suspended).Should().BeTrue();
    }

    [Fact]
    public async Task EventGrain_MarketManagement_ShouldHandleComplexMarketOperations()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);

        var market1 = await grain.AddMarketAsync(
            "Market 1",
            "First market",
            new Dictionary<string, decimal> { { "outcome1", 1.50m }, { "outcome2", 2.50m } });

        var market2 = await grain.AddMarketAsync(
            "Market 2",
            "Second market",
            new Dictionary<string, decimal> { { "yes", 1.80m }, { "no", 2.00m } });

        var suspendedMarket1 = await grain.UpdateMarketStatusAsync(market1.Id, MarketStatus.Suspended);
        suspendedMarket1.Status.Should().Be(MarketStatus.Suspended);

        var closedMarket1 = await grain.UpdateMarketStatusAsync(market1.Id, MarketStatus.Open);
        closedMarket1.Status.Should().Be(MarketStatus.Open);
        await grain.UpdateMarketStatusAsync(market1.Id, MarketStatus.Closed);

        var settledMarket1 = await grain.SetMarketResultAsync(market1.Id, "outcome1");
        settledMarket1.Status.Should().Be(MarketStatus.Settled);
        settledMarket1.WinningOutcome.Should().Be("outcome1");

        var allMarkets = await grain.GetMarketsAsync();
        allMarkets.Should().HaveCount(2);
        allMarkets.Single(m => m.Id == market1.Id).Status.Should().Be(MarketStatus.Settled);
        allMarkets.Single(m => m.Id == market2.Id).Status.Should().Be(MarketStatus.Open);
    }

    [Fact]
    public async Task EventGrain_ConcurrentMarketOperations_ShouldMaintainConsistency()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);

        var marketTasks = new List<Task<Market>>();
        for (int i = 0; i < 10; i++)
        {
            var outcomes = new Dictionary<string, decimal> { { $"outcome{i}_1", 1.50m + i * 0.1m }, { $"outcome{i}_2", 2.50m - i * 0.1m } };
            marketTasks.Add(grain.AddMarketAsync($"Concurrent Market {i}", $"Description {i}", outcomes).AsTask());
        }

        var markets = await Task.WhenAll(marketTasks);

        markets.Should().HaveCount(10);
        markets.Select(m => m.Id).Should().OnlyHaveUniqueItems();
        markets.Select(m => m.Name).Should().OnlyHaveUniqueItems();

        var allMarkets = await grain.GetMarketsAsync();
        allMarkets.Should().HaveCount(10);
        allMarkets.All(m => m.Status == MarketStatus.Open).Should().BeTrue();
    }

    [Fact]
    public async Task EventGrain_EventUpdateOperations_ShouldHandlePartialUpdates()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId, "Original Event");

        var updatedWithName = await grain.UpdateEventAsync(name: "Updated Event Name");
        updatedWithName.Name.Should().Be("Updated Event Name");
        updatedWithName.StartTime.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(1), TimeSpan.FromMinutes(1));

        var newStartTime = DateTimeOffset.UtcNow.AddDays(3);
        var updatedWithTime = await grain.UpdateEventAsync(startTime: newStartTime);
        updatedWithTime.Name.Should().Be("Updated Event Name");
        updatedWithTime.StartTime.Should().Be(newStartTime);

        var newParticipants = new Dictionary<string, string> { { "home", "New Team A" }, { "away", "New Team B" } };
        var updatedWithParticipants = await grain.UpdateEventAsync(participants: newParticipants);
        updatedWithParticipants.Participants.Should().BeEquivalentTo(newParticipants);
        updatedWithParticipants.Name.Should().Be("Updated Event Name");
        updatedWithParticipants.StartTime.Should().Be(newStartTime);
    }

    [Fact]
    public async Task EventGrain_CancellationWithMarkets_ShouldSuspendAllMarkets()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);

        await grain.AddMarketAsync("Market 1", "Description 1", new Dictionary<string, decimal> { { "home", 1.85m } });
        await grain.AddMarketAsync("Market 2", "Description 2", new Dictionary<string, decimal> { { "away", 2.10m } });
        var market3 = await grain.AddMarketAsync("Market 3", "Description 3", new Dictionary<string, decimal> { { "draw", 3.40m } });
        await grain.UpdateMarketStatusAsync(market3.Id, MarketStatus.Suspended);

        await grain.CancelEventAsync("Weather conditions");

        var eventDetails = await grain.GetEventDetailsAsync();
        var markets = await grain.GetMarketsAsync();

        eventDetails.Status.Should().Be(EventStatus.Cancelled);
        markets.All(m => m.Status == MarketStatus.Suspended).Should().BeTrue();
    }

    [Fact]
    public async Task EventGrain_ResultManagement_ShouldHandleComplexResults()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);

        var complexResults = new Dictionary<string, object>
        {
            { "home_score", 3 },
            { "away_score", 2 },
            { "total_goals", 5 },
            { "first_half_score", "2-1" },
            { "winner", "home" },
            { "match_duration_minutes", 94 },
            { "red_cards", 1 },
            { "corners", new Dictionary<string, int> { { "home", 7 }, { "away", 4 } } },
            { "possession_percentage", new Dictionary<string, decimal> { { "home", 55.2m }, { "away", 44.8m } } }
        };

        var result = await grain.SetResultAsync(complexResults, true);

        result.EventId.Should().Be(eventId);
        result.IsOfficial.Should().BeTrue();
        result.Results.Should().HaveCount(8);
        result.Results["home_score"].Should().Be(3);
        result.Results["away_score"].Should().Be(2);
        result.Results["winner"].Should().Be("home");

        var corners = result.Results["corners"] as Dictionary<string, int>;
        corners.Should().NotBeNull();
        corners!["home"].Should().Be(7);
        corners["away"].Should().Be(4);

        var storedResult = await grain.GetResultAsync();
        storedResult.Should().NotBeNull();
        storedResult!.Value.Should().Be(result);
    }

    [Fact]
    public async Task EventGrain_MultipleGrainInstances_ShouldIsolateState()
    {
        var event1Id = Guid.NewGuid();
        var event2Id = Guid.NewGuid();
        var grain1 = await CreateTestEventAsync(event1Id, "Event 1");
        var grain2 = await CreateTestEventAsync(event2Id, "Event 2");

        await grain1.AddMarketAsync("Event 1 Market", "Description", new Dictionary<string, decimal> { { "home", 1.85m } });
        await grain2.AddMarketAsync("Event 2 Market", "Description", new Dictionary<string, decimal> { { "away", 2.10m } });

        await grain1.StartEventAsync();

        var event1Details = await grain1.GetEventDetailsAsync();
        var event2Details = await grain2.GetEventDetailsAsync();
        var event1Markets = await grain1.GetMarketsAsync();
        var event2Markets = await grain2.GetMarketsAsync();

        event1Details.Id.Should().Be(event1Id);
        event1Details.Name.Should().Be("Event 1");
        event1Details.Status.Should().Be(EventStatus.Live);

        event2Details.Id.Should().Be(event2Id);
        event2Details.Name.Should().Be("Event 2");
        event2Details.Status.Should().Be(EventStatus.Scheduled);

        event1Markets.Should().HaveCount(1);
        event1Markets.First().Name.Should().Be("Event 1 Market");

        event2Markets.Should().HaveCount(1);
        event2Markets.First().Name.Should().Be("Event 2 Market");
    }

    [Fact]
    public async Task EventGrain_ErrorScenarios_ShouldHandleGracefully()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);

        var market = await grain.AddMarketAsync("Test Market", "Description", new Dictionary<string, decimal> { { "home", 1.85m }, { "away", 2.10m } });

        var act = async () => await grain.SetMarketResultAsync(market.Id, "invalid_outcome");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Invalid winning outcome: invalid_outcome");

        await grain.UpdateMarketStatusAsync(market.Id, MarketStatus.Closed);
        var settledMarket = await grain.SetMarketResultAsync(market.Id, "home");
        settledMarket.WinningOutcome.Should().Be("home");

        var secondSettlement = async () => await grain.SetMarketResultAsync(market.Id, "away");
        await secondSettlement.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task EventGrain_LargeScaleOperations_ShouldPerformWell()
    {
        var eventId = Guid.NewGuid();
        var grain = await CreateTestEventAsync(eventId);

        var startTime = DateTimeOffset.UtcNow;

        var marketTasks = new List<Task<Market>>();
        for (int i = 0; i < 50; i++)
        {
            var outcomes = new Dictionary<string, decimal>();
            for (int j = 0; j < 10; j++)
            {
                outcomes[$"outcome_{i}_{j}"] = 1.50m + (j * 0.1m);
            }
            marketTasks.Add(grain.AddMarketAsync($"Market {i}", $"Description for market {i}", outcomes).AsTask());
        }

        var markets = await Task.WhenAll(marketTasks);
        var endTime = DateTimeOffset.UtcNow;

        var duration = endTime - startTime;
        duration.Should().BeLessThan(TimeSpan.FromSeconds(30));

        markets.Should().HaveCount(50);
        markets.Select(m => m.Id).Should().OnlyHaveUniqueItems();

        var allMarkets = await grain.GetMarketsAsync();
        allMarkets.Should().HaveCount(50);
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