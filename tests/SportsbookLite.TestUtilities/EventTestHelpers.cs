using FluentAssertions;
using Orleans;
using SportsbookLite.Contracts.Events;
using SportsbookLite.GrainInterfaces;
using SportsbookLite.TestUtilities.TestDataBuilders;

namespace SportsbookLite.TestUtilities;

public static class EventTestHelpers
{
    public static async Task<ISportEventGrain> CreateTestEventAsync(
        IGrainFactory grainFactory,
        Guid? eventId = null,
        string name = "Test Event",
        SportType sportType = SportType.Soccer,
        string competition = "Test League",
        DateTimeOffset? startTime = null,
        Dictionary<string, string>? participants = null)
    {
        var grain = grainFactory.GetGrain<ISportEventGrain>(eventId ?? Guid.NewGuid());
        
        var eventParticipants = participants ?? new Dictionary<string, string>
        {
            { "home", "Team A" },
            { "away", "Team B" }
        };

        await grain.CreateEventAsync(
            name,
            sportType,
            competition,
            startTime ?? DateTimeOffset.UtcNow.AddDays(1),
            eventParticipants);

        return grain;
    }

    public static async Task<Market> CreateTestMarketAsync(
        ISportEventGrain eventGrain,
        string name = "Test Market",
        string description = "Test market description",
        Dictionary<string, decimal>? outcomes = null)
    {
        var marketOutcomes = outcomes ?? new Dictionary<string, decimal>
        {
            { "home", 1.85m },
            { "away", 2.10m }
        };

        return await eventGrain.AddMarketAsync(name, description, marketOutcomes);
    }

    public static async Task<ISportEventGrain> CreateEventWithMarketsAsync(
        IGrainFactory grainFactory,
        Guid? eventId = null,
        int marketCount = 3,
        string eventName = "Test Event with Markets")
    {
        var grain = await CreateTestEventAsync(grainFactory, eventId, eventName);

        for (int i = 0; i < marketCount; i++)
        {
            await CreateTestMarketAsync(grain, $"Market {i + 1}", $"Description {i + 1}");
        }

        return grain;
    }

    public static async Task<ISportEventGrain> CreateLiveEventAsync(
        IGrainFactory grainFactory,
        Guid? eventId = null,
        string name = "Live Test Event")
    {
        var grain = await CreateTestEventAsync(grainFactory, eventId, name);
        await grain.StartEventAsync();
        return grain;
    }

    public static async Task<ISportEventGrain> CreateCompletedEventAsync(
        IGrainFactory grainFactory,
        Guid? eventId = null,
        string name = "Completed Test Event",
        EventResult? result = null)
    {
        var grain = await CreateTestEventAsync(grainFactory, eventId, name);
        await grain.StartEventAsync();
        await grain.CompleteEventAsync(result);
        return grain;
    }

    public static async Task<ISportEventGrain> CreateCancelledEventAsync(
        IGrainFactory grainFactory,
        Guid? eventId = null,
        string name = "Cancelled Test Event",
        string reason = "Test cancellation")
    {
        var grain = await CreateTestEventAsync(grainFactory, eventId, name);
        await grain.CancelEventAsync(reason);
        return grain;
    }

    public static async Task AssertEventStatusAsync(
        ISportEventGrain grain,
        EventStatus expectedStatus)
    {
        var eventDetails = await grain.GetEventDetailsAsync();
        eventDetails.Status.Should().Be(expectedStatus, $"Event should be in {expectedStatus} status");
    }

    public static async Task AssertEventHasMarketsAsync(
        ISportEventGrain grain,
        int expectedCount)
    {
        var markets = await grain.GetMarketsAsync();
        markets.Should().HaveCount(expectedCount, $"Event should have {expectedCount} markets");
    }

    public static async Task AssertMarketStatusAsync(
        ISportEventGrain grain,
        Guid marketId,
        MarketStatus expectedStatus)
    {
        var markets = await grain.GetMarketsAsync();
        var market = markets.FirstOrDefault(m => m.Id == marketId);
        market.Should().NotBeNull("Market should exist");
        market!.Status.Should().Be(expectedStatus, $"Market should be in {expectedStatus} status");
    }

    public static async Task AssertAllMarketsStatusAsync(
        ISportEventGrain grain,
        MarketStatus expectedStatus)
    {
        var markets = await grain.GetMarketsAsync();
        markets.Should().AllSatisfy(m => 
            m.Status.Should().Be(expectedStatus, $"All markets should be in {expectedStatus} status"));
    }

    public static async Task AssertEventResultAsync(
        ISportEventGrain grain,
        string resultKey,
        object expectedValue)
    {
        var result = await grain.GetResultAsync();
        result.Should().NotBeNull("Event should have a result");
        result!.Value.Results.Should().ContainKey(resultKey, $"Result should contain key '{resultKey}'");
        result.Value.Results[resultKey].Should().Be(expectedValue, $"Result value for '{resultKey}' should match");
    }

    public static void AssertEventTransition(
        EventStatus fromStatus,
        EventStatus toStatus,
        bool shouldBeAllowed)
    {
        var testEvent = EventTestDataBuilder.Create()
            .WithStatus(fromStatus)
            .Build();

        var canTransition = testEvent.CanTransitionTo(toStatus);
        canTransition.Should().Be(shouldBeAllowed,
            $"Transition from {fromStatus} to {toStatus} should {(shouldBeAllowed ? "be allowed" : "not be allowed")}");
    }

    public static void AssertMarketTransition(
        MarketStatus fromStatus,
        MarketStatus toStatus,
        bool shouldBeAllowed)
    {
        var testMarket = MarketTestDataBuilder.Create()
            .WithStatus(fromStatus)
            .Build();

        var canTransition = testMarket.CanTransitionTo(toStatus);
        canTransition.Should().Be(shouldBeAllowed,
            $"Transition from {fromStatus} to {toStatus} should {(shouldBeAllowed ? "be allowed" : "not be allowed")}");
    }

    public static void AssertSportEvent(
        SportEvent actual,
        string expectedName,
        SportType expectedSportType,
        string expectedCompetition,
        EventStatus expectedStatus,
        Dictionary<string, string>? expectedParticipants = null)
    {
        actual.Name.Should().Be(expectedName, "Event name should match");
        actual.SportType.Should().Be(expectedSportType, "Sport type should match");
        actual.Competition.Should().Be(expectedCompetition, "Competition should match");
        actual.Status.Should().Be(expectedStatus, "Event status should match");

        if (expectedParticipants != null)
        {
            actual.Participants.Should().BeEquivalentTo(expectedParticipants, "Participants should match");
        }

        actual.Id.Should().NotBeEmpty("Event should have a valid ID");
        actual.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), "Event should have been created recently");
        actual.LastModified.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), "Event should have been modified recently");
    }

    public static void AssertMarket(
        Market actual,
        Guid expectedEventId,
        string expectedName,
        string expectedDescription,
        MarketStatus expectedStatus,
        Dictionary<string, decimal>? expectedOutcomes = null,
        string? expectedWinner = null)
    {
        actual.EventId.Should().Be(expectedEventId, "Market event ID should match");
        actual.Name.Should().Be(expectedName, "Market name should match");
        actual.Description.Should().Be(expectedDescription, "Market description should match");
        actual.Status.Should().Be(expectedStatus, "Market status should match");
        actual.WinningOutcome.Should().Be(expectedWinner, "Market winner should match");

        if (expectedOutcomes != null)
        {
            actual.Outcomes.Should().BeEquivalentTo(expectedOutcomes, "Market outcomes should match");
        }

        actual.Id.Should().NotBeEmpty("Market should have a valid ID");
        actual.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), "Market should have been created recently");
        actual.LastModified.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), "Market should have been modified recently");
    }

    public static void AssertEventResult(
        EventResult actual,
        Guid expectedEventId,
        bool expectedIsOfficial,
        Dictionary<string, object> expectedResults)
    {
        actual.EventId.Should().Be(expectedEventId, "Result event ID should match");
        actual.IsOfficial.Should().Be(expectedIsOfficial, "Result official status should match");
        actual.Results.Should().BeEquivalentTo(expectedResults, "Result data should match");
        actual.ResultTime.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), "Result should have been created recently");
    }

    public static async Task<List<SportEvent>> GetAllEventsFromGrainsAsync(
        IGrainFactory grainFactory,
        IEnumerable<Guid> eventIds)
    {
        var events = new List<SportEvent>();
        
        foreach (var eventId in eventIds)
        {
            var grain = grainFactory.GetGrain<ISportEventGrain>(eventId);
            try
            {
                var eventDetails = await grain.GetEventDetailsAsync();
                events.Add(eventDetails);
            }
            catch (InvalidOperationException)
            {
            }
        }
        
        return events;
    }

    public static async Task<Dictionary<Guid, List<Market>>> GetAllMarketsFromEventsAsync(
        IGrainFactory grainFactory,
        IEnumerable<Guid> eventIds)
    {
        var eventMarkets = new Dictionary<Guid, List<Market>>();
        
        foreach (var eventId in eventIds)
        {
            var grain = grainFactory.GetGrain<ISportEventGrain>(eventId);
            try
            {
                var markets = await grain.GetMarketsAsync();
                eventMarkets[eventId] = markets.ToList();
            }
            catch (InvalidOperationException)
            {
                eventMarkets[eventId] = new List<Market>();
            }
        }
        
        return eventMarkets;
    }

    public static class Scenarios
    {
        public static async Task<ISportEventGrain> CreatePremierLeagueMatchAsync(
            IGrainFactory grainFactory,
            string homeTeam = "Liverpool",
            string awayTeam = "Arsenal",
            Guid? eventId = null)
        {
            return await CreateTestEventAsync(
                grainFactory,
                eventId,
                $"{homeTeam} vs {awayTeam}",
                SportType.Soccer,
                "Premier League",
                DateTimeOffset.UtcNow.AddDays(1),
                new Dictionary<string, string> { { "home", homeTeam }, { "away", awayTeam } });
        }

        public static async Task<ISportEventGrain> CreateNBAGameAsync(
            IGrainFactory grainFactory,
            string homeTeam = "Lakers",
            string awayTeam = "Warriors",
            Guid? eventId = null)
        {
            return await CreateTestEventAsync(
                grainFactory,
                eventId,
                $"{homeTeam} vs {awayTeam}",
                SportType.Basketball,
                "NBA",
                DateTimeOffset.UtcNow.AddHours(8),
                new Dictionary<string, string> { { "home", homeTeam }, { "away", awayTeam } });
        }

        public static async Task<ISportEventGrain> CreateTennisMatchAsync(
            IGrainFactory grainFactory,
            string player1 = "Djokovic",
            string player2 = "Nadal",
            Guid? eventId = null)
        {
            return await CreateTestEventAsync(
                grainFactory,
                eventId,
                $"{player1} vs {player2}",
                SportType.Tennis,
                "Wimbledon",
                DateTimeOffset.UtcNow.AddHours(24),
                new Dictionary<string, string> { { "player1", player1 }, { "player2", player2 } });
        }

        public static async Task<ISportEventGrain> CreateCompleteMatchWithResultAsync(
            IGrainFactory grainFactory,
            Guid? eventId = null,
            string homeTeam = "Team A",
            string awayTeam = "Team B",
            int homeScore = 2,
            int awayScore = 1)
        {
            var grain = await CreatePremierLeagueMatchAsync(grainFactory, homeTeam, awayTeam, eventId);
            await grain.StartEventAsync();
            
            var result = EventResult.Create(
                grain.GetPrimaryKey(),
                new Dictionary<string, object>
                {
                    { "home_score", homeScore },
                    { "away_score", awayScore },
                    { "winner", homeScore > awayScore ? "home" : awayScore > homeScore ? "away" : "draw" }
                });
                
            await grain.CompleteEventAsync(result);
            return grain;
        }
    }

    public static class Assertions
    {
        public static void EventCreationShouldSucceed(SportEvent sportEvent, string expectedName, SportType expectedSportType)
        {
            AssertSportEvent(sportEvent, expectedName, expectedSportType, "Test League", EventStatus.Scheduled);
        }

        public static void MarketCreationShouldSucceed(Market market, Guid expectedEventId, string expectedName)
        {
            AssertMarket(market, expectedEventId, expectedName, "Test market description", MarketStatus.Open);
        }

        public static async Task EventLifecycleShouldWork(ISportEventGrain grain)
        {
            await AssertEventStatusAsync(grain, EventStatus.Scheduled);
            
            await grain.StartEventAsync();
            await AssertEventStatusAsync(grain, EventStatus.Live);
            
            await grain.CompleteEventAsync();
            await AssertEventStatusAsync(grain, EventStatus.Completed);
        }

        public static async Task MarketSettlementShouldWork(ISportEventGrain grain, Guid marketId, string winningOutcome)
        {
            await grain.UpdateMarketStatusAsync(marketId, MarketStatus.Closed);
            await AssertMarketStatusAsync(grain, marketId, MarketStatus.Closed);
            
            await grain.SetMarketResultAsync(marketId, winningOutcome);
            await AssertMarketStatusAsync(grain, marketId, MarketStatus.Settled);
            
            var markets = await grain.GetMarketsAsync();
            var settledMarket = markets.First(m => m.Id == marketId);
            settledMarket.WinningOutcome.Should().Be(winningOutcome);
        }
    }
}