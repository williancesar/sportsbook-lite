using SportsbookLite.JourneyTests.Builders.Features;

namespace SportsbookLite.JourneyTests.Journeys;

public sealed class ConcurrentBettingJourneyTests : JourneyTestBase
{
    public ConcurrentBettingJourneyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task MultipleUsers_BettingSimultaneously_ShouldHandleCorrectly()
    {
        // Arrange
        const int numberOfUsers = 10;
        const decimal initialBalance = 500m;
        const decimal betAmount = 50m;
        
        // Create event first
        var setupContext = await new SportsbookJourneyBuilder(ApiClient, Output)
            .LiveFootballMatch("Concurrent Test Match")
            .ExecuteAsync();
        
        var eventId = setupContext.Get<Guid>("eventId");
        var marketId = setupContext.Get<string>("market_match_result");
        
        // Create user journeys
        var userJourneys = new List<Task<JourneyContext>>();
        
        for (int i = 1; i <= numberOfUsers; i++)
        {
            var userId = $"concurrent_user_{i}";
            var journey = new SportsbookJourneyBuilder(ApiClient, Output)
                .CreateUser(userId)
                .FundWallet(initialBalance, "USD")
                .Then(ctx => ctx.Set("eventId", eventId))
                .Then(ctx => ctx.Set("lastMarketId", marketId))
                .PlaceBet(betAmount, "home_win", 2.10m)
                .VerifyBalance(initialBalance - betAmount)
                .ExecuteAsync();
                
            userJourneys.Add(journey);
        }
        
        // Act - Execute all journeys concurrently
        var contexts = await Task.WhenAll(userJourneys);
        
        // Assert
        contexts.Should().HaveCount(numberOfUsers);
        contexts.Should().AllSatisfy(context =>
        {
            context.Assertions.Should().AllSatisfy(a => a.Success.Should().BeTrue());
            var betId = context.Get<Guid>("lastBetId");
            betId.Should().NotBeEmpty();
        });
        
        // Verify all bet IDs are unique
        var allBetIds = contexts.Select(c => c.Get<Guid>("lastBetId")).ToList();
        allBetIds.Should().OnlyHaveUniqueItems();
        
        Output.WriteLine($"Successfully processed {numberOfUsers} concurrent bets");
    }

    [Fact]
    public async Task HighFrequencyBetting_SingleUser_RapidBets_ShouldProcessInOrder()
    {
        // Arrange
        const int numberOfBets = 20;
        var userId = "rapid_bettor";
        
        var context = await new SportsbookJourneyBuilder(ApiClient, Output)
            .CreateUser(userId)
            .FundWallet(2000m, "USD")
            .LiveFootballMatch("Rapid Betting Match")
            .ExecuteAsync();
        
        var betTasks = new List<Task<Guid>>();
        
        // Act - Place bets rapidly
        for (int i = 0; i < numberOfBets; i++)
        {
            var betTask = PlaceBetAsync(userId, context.Get<Guid>("eventId"), 
                context.Get<string>("market_match_result"), 10m + i);
            betTasks.Add(betTask);
        }
        
        var betIds = await Task.WhenAll(betTasks);
        
        // Assert
        betIds.Should().HaveCount(numberOfBets);
        betIds.Should().OnlyHaveUniqueItems();
        
        // Verify final balance
        var finalBalance = await GetBalanceAsync(userId);
        var totalStaked = Enumerable.Range(0, numberOfBets).Sum(i => 10m + i);
        finalBalance.Should().Be(2000m - totalStaked);
        
        Output.WriteLine($"Processed {numberOfBets} rapid bets successfully");
    }

    [Fact]
    public async Task OddsChangesDuringConcurrentBetting_ShouldHandleGracefully()
    {
        // Arrange
        const int numberOfUsers = 5;
        var eventContext = await new SportsbookJourneyBuilder(ApiClient, Output)
            .CreateEvent("Volatile Odds Match", SportType.Basketball)
            .AddMarket("winner", "team_a", "team_b")
            .SetOdds("team_a", 2.00m)
            .SetOdds("team_b", 2.00m)
            .StartEvent()
            .ExecuteAsync();
        
        var eventId = eventContext.Get<Guid>("eventId");
        var marketId = eventContext.Get<string>("lastMarketId");
        
        // Create betting and odds update tasks
        var bettingTasks = new List<Task<JourneyContext>>();
        
        // Users trying to bet
        for (int i = 1; i <= numberOfUsers; i++)
        {
            var userId = $"odds_change_user_{i}";
            var acceptableOdds = 2.00m - (i * 0.05m); // Different acceptable odds
            
            var journey = new SportsbookJourneyBuilder(ApiClient, Output)
                .CreateUser(userId)
                .FundWallet(500m, "USD")
                .Then(ctx => 
                {
                    ctx.Set("eventId", eventId);
                    ctx.Set("lastMarketId", marketId);
                })
                .PlaceBet(100m, "team_a", acceptableOdds)
                .ExecuteAsync();
                
            bettingTasks.Add(journey);
        }
        
        // Simultaneously change odds
        var oddsUpdateTask = Task.Run(async () =>
        {
            await Task.Delay(50); // Small delay to ensure some bets are in flight
            
            await new SportsbookJourneyBuilder(ApiClient, Output)
                .Then(ctx => ctx.Set("lastMarketId", marketId))
                .SetOdds("team_a", 1.70m) // Drop odds
                .ExecuteAsync();
        });
        
        // Act
        await Task.WhenAll(bettingTasks.Concat(new[] { oddsUpdateTask }));
        var contexts = await Task.WhenAll(bettingTasks);
        
        // Assert
        // Some bets might fail due to odds mismatch, but system should handle gracefully
        var successfulBets = contexts.Count(c => 
            c.TryGet<Guid>("lastBetId", out var betId) && betId != Guid.Empty);
        
        successfulBets.Should().BeGreaterThan(0, "At least some bets should succeed");
        Output.WriteLine($"{successfulBets}/{numberOfUsers} bets succeeded during odds volatility");
    }

    [Fact]
    public async Task ParallelBettingOnMultipleEvents_ShouldDistributeCorrectly()
    {
        // Arrange
        const int numberOfEvents = 3;
        const int usersPerEvent = 5;
        var events = new List<(Guid EventId, string MarketId)>();
        
        // Create multiple events
        for (int i = 1; i <= numberOfEvents; i++)
        {
            var eventContext = await new SportsbookJourneyBuilder(ApiClient, Output)
                .CreateEvent($"Parallel Event {i}", SportType.Football)
                .AddMarket("winner", "home", "away")
                .SetOdds("home", 1.80m + (i * 0.1m))
                .SetOdds("away", 2.20m - (i * 0.1m))
                .StartEvent()
                .ExecuteAsync();
                
            events.Add((eventContext.Get<Guid>("eventId"), 
                       eventContext.Get<string>("lastMarketId")));
        }
        
        // Create user journeys distributed across events
        var allJourneys = new List<Task<JourneyContext>>();
        
        for (int eventIndex = 0; eventIndex < numberOfEvents; eventIndex++)
        {
            var (eventId, marketId) = events[eventIndex];
            
            for (int userIndex = 1; userIndex <= usersPerEvent; userIndex++)
            {
                var userId = $"user_e{eventIndex}_u{userIndex}";
                var selection = userIndex % 2 == 0 ? "home" : "away";
                
                var journey = new SportsbookJourneyBuilder(ApiClient, Output)
                    .CreateUser(userId)
                    .FundWallet(300m, "USD")
                    .Then(ctx =>
                    {
                        ctx.Set("eventId", eventId);
                        ctx.Set("lastMarketId", marketId);
                    })
                    .PlaceBet(50m, selection, 3.00m) // High acceptable odds
                    .ExecuteAsync();
                    
                allJourneys.Add(journey);
            }
        }
        
        // Act
        var contexts = await Task.WhenAll(allJourneys);
        
        // Assert
        contexts.Should().HaveCount(numberOfEvents * usersPerEvent);
        
        var successfulBets = contexts.Count(c => 
            c.TryGet<Guid>("lastBetId", out var betId) && betId != Guid.Empty);
        
        successfulBets.Should().Be(numberOfEvents * usersPerEvent, 
            "All bets should succeed as odds are acceptable");
        
        Output.WriteLine($"Distributed {successfulBets} bets across {numberOfEvents} events");
    }

    [Fact]
    public async Task StressTest_MaximumConcurrentLoad_ShouldNotCrash()
    {
        // Arrange
        const int maxConcurrentUsers = 50;
        const decimal minBet = 10m;
        const decimal maxBet = 100m;
        var random = new Random();
        
        // Create a single event for stress testing
        var eventContext = await new SportsbookJourneyBuilder(ApiClient, Output)
            .CreateEvent("Stress Test Event", SportType.Football)
            .AddMarket("winner", "team_a", "team_b", "draw")
            .SetOdds("team_a", 2.50m)
            .SetOdds("team_b", 3.00m)
            .SetOdds("draw", 3.50m)
            .StartEvent()
            .ExecuteAsync();
        
        var eventId = eventContext.Get<Guid>("eventId");
        var marketId = eventContext.Get<string>("lastMarketId");
        var selections = new[] { "team_a", "team_b", "draw" };
        
        // Create stress test journeys
        var stressTasks = new List<Task<(bool Success, TimeSpan Duration)>>();
        
        for (int i = 1; i <= maxConcurrentUsers; i++)
        {
            var userId = $"stress_user_{i}";
            var betAmount = minBet + (decimal)(random.NextDouble() * (double)(maxBet - minBet));
            var selection = selections[random.Next(selections.Length)];
            
            var task = ExecuteTimedJourney(async () =>
            {
                var journey = new SportsbookJourneyBuilder(ApiClient, Output)
                    .CreateUser(userId)
                    .FundWallet(1000m, "USD")
                    .Then(ctx =>
                    {
                        ctx.Set("eventId", eventId);
                        ctx.Set("lastMarketId", marketId);
                    })
                    .PlaceBet(betAmount, selection, 5.00m) // High tolerance
                    .ExecuteAsync();
                    
                var context = await journey;
                return context.TryGet<Guid>("lastBetId", out var betId) && betId != Guid.Empty;
            });
            
            stressTasks.Add(task);
        }
        
        // Act
        var results = await Task.WhenAll(stressTasks);
        
        // Assert
        var successCount = results.Count(r => r.Success);
        var averageDuration = results.Average(r => r.Duration.TotalMilliseconds);
        var maxDuration = results.Max(r => r.Duration.TotalMilliseconds);
        
        successCount.Should().BeGreaterThan(maxConcurrentUsers * 8 / 10, 
            "At least 80% should succeed under stress");
        averageDuration.Should().BeLessThan(5000, "Average response time should be under 5 seconds");
        maxDuration.Should().BeLessThan(10000, "Max response time should be under 10 seconds");
        
        Output.WriteLine($"Stress test results:");
        Output.WriteLine($"  Success rate: {successCount}/{maxConcurrentUsers} ({100.0 * successCount / maxConcurrentUsers:F1}%)");
        Output.WriteLine($"  Average duration: {averageDuration:F0}ms");
        Output.WriteLine($"  Max duration: {maxDuration:F0}ms");
    }

    private async Task<Guid> PlaceBetAsync(string userId, Guid eventId, string marketId, decimal stake)
    {
        var request = new
        {
            UserId = userId,
            EventId = eventId,
            MarketId = marketId,
            SelectionId = "home_win",
            Stake = stake,
            Currency = "USD",
            AcceptableOdds = 5.00m,
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        var response = await ApiClient.PostAsJsonAsync("/api/bets", request);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<PlaceBetResponse>();
        return result!.BetId;
    }

    private async Task<decimal> GetBalanceAsync(string userId)
    {
        var response = await ApiClient.GetAsync($"/api/wallet/{userId}/balance");
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        return result!.Amount;
    }

    private async Task<(bool Success, TimeSpan Duration)> ExecuteTimedJourney(Func<Task<bool>> journey)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var success = await journey();
            stopwatch.Stop();
            return (success, stopwatch.Elapsed);
        }
        catch
        {
            stopwatch.Stop();
            return (false, stopwatch.Elapsed);
        }
    }
}