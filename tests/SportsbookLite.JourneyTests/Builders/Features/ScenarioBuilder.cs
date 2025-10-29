namespace SportsbookLite.JourneyTests.Builders.Features;

public static class Scenarios
{
    public static SportsbookJourneyBuilder StandardUserWithBalance(
        this SportsbookJourneyBuilder builder,
        string userId,
        decimal balance = 1000m)
    {
        return builder
            .CreateUser(userId)
            .FundWallet(balance, "USD");
    }

    public static SportsbookJourneyBuilder LiveFootballMatch(
        this SportsbookJourneyBuilder builder,
        string eventName = "Premier League Match")
    {
        return builder
            .CreateEvent(eventName, SportType.Football, DateTimeOffset.UtcNow.AddMinutes(5))
            .AddMarket("match_result", "home_win", "draw", "away_win")
            .SetOdds("home_win", 2.10m)
            .SetOdds("draw", 3.20m)
            .SetOdds("away_win", 3.50m)
            .AddMarket("total_goals", "over_2.5", "under_2.5")
            .SetOdds("over_2.5", 1.85m)
            .SetOdds("under_2.5", 1.95m)
            .StartEvent();
    }

    public static SportsbookJourneyBuilder HighVolatilityMarket(
        this SportsbookJourneyBuilder builder,
        string eventName = "High Stakes Match")
    {
        return builder
            .CreateEvent(eventName, SportType.Basketball)
            .AddMarket("winner", "team_a", "team_b")
            .SetOdds("team_a", 1.50m)
            .SetOdds("team_b", 2.50m)
            .StartEvent()
            .Delay(100)
            .SetOdds("team_a", 1.40m)  // Odds dropping
            .Delay(100)
            .SetOdds("team_a", 1.30m)  // Further drop
            .Delay(100)
            .SetOdds("team_a", 1.60m); // Correction
    }

    public static SportsbookJourneyBuilder SettlementReadyEvent(
        this SportsbookJourneyBuilder builder,
        string winningSelection = "home_win")
    {
        return builder
            .CreateEvent("Settlement Test Match", SportType.Football)
            .AddMarket("match_result", "home_win", "draw", "away_win")
            .SetOdds("home_win", 2.00m)
            .SetOdds("draw", 3.00m)
            .SetOdds("away_win", 4.00m)
            .StartEvent()
            .Delay(500) // Simulate match duration
            .CompleteEvent(winningSelection);
    }

    public static SportsbookJourneyBuilder MultiMarketEvent(
        this SportsbookJourneyBuilder builder,
        string eventName = "Multi-Market Match")
    {
        return builder
            .CreateEvent(eventName, SportType.Football)
            .AddMarket("match_result", "home_win", "draw", "away_win")
            .SetOdds("home_win", 2.10m)
            .SetOdds("draw", 3.20m)
            .SetOdds("away_win", 3.50m)
            .AddMarket("both_teams_score", "yes", "no")
            .SetOdds("yes", 1.70m)
            .SetOdds("no", 2.10m)
            .AddMarket("total_goals", "over_2.5", "under_2.5")
            .SetOdds("over_2.5", 1.85m)
            .SetOdds("under_2.5", 1.95m)
            .AddMarket("first_goal", "home", "away", "none")
            .SetOdds("home", 2.20m)
            .SetOdds("away", 3.10m)
            .SetOdds("none", 9.00m);
    }

    public static SportsbookJourneyBuilder QuickBettingScenario(
        this SportsbookJourneyBuilder builder,
        string userId,
        decimal stake = 100m)
    {
        return builder
            .StandardUserWithBalance(userId, 1000m)
            .LiveFootballMatch()
            .PlaceBet(stake, "home_win", 2.10m);
    }

    public static SportsbookJourneyBuilder CashoutEligibleBet(
        this SportsbookJourneyBuilder builder,
        string userId)
    {
        return builder
            .StandardUserWithBalance(userId, 500m)
            .CreateEvent("Cashout Test Match", SportType.Tennis)
            .AddMarket("winner", "player_a", "player_b")
            .SetOdds("player_a", 2.50m)
            .SetOdds("player_b", 1.50m)
            .StartEvent()
            .PlaceBet(100m, "player_a", 2.50m)
            .Delay(1000)
            .SetOdds("player_a", 1.80m); // Odds improved for cashout
    }

    public static SportsbookJourneyBuilder ParallelBettingSetup(
        this SportsbookJourneyBuilder builder,
        int numberOfUsers = 5)
    {
        builder = builder.LiveFootballMatch();
        
        for (int i = 1; i <= numberOfUsers; i++)
        {
            var userId = $"parallel_user_{i}";
            builder = builder
                .ForUser(userId)
                .FundWallet(500m, "USD");
        }
        
        return builder;
    }

    public static SportsbookJourneyBuilder ErrorProneScenario(
        this SportsbookJourneyBuilder builder,
        string userId)
    {
        return builder
            .CreateUser(userId)
            .FundWallet(100m, "USD")
            .CreateEvent("Error Test Match", SportType.Football)
            .AddMarket("match_result", "home_win", "draw", "away_win")
            .SetOdds("home_win", 2.00m)
            // Intentionally trying to bet more than balance
            .PlaceBet(200m, "home_win", 2.00m); // Should fail
    }

    public static SportsbookJourneyBuilder AdminEventManagement(
        this SportsbookJourneyBuilder builder,
        string eventName = "Admin Managed Match")
    {
        return builder
            .AsAdministrator()
            .CreateEvent(eventName, SportType.Football)
            .AddMarket("match_result", "home_win", "draw", "away_win")
            .SetOdds("home_win", 2.10m)
            .SetOdds("draw", 3.20m)
            .SetOdds("away_win", 3.50m)
            .Then(ctx => ctx.RecordStep("Event ready for betting"));
    }

    public static SportsbookJourneyBuilder CompleteUserJourney(
        this SportsbookJourneyBuilder builder,
        string userId)
    {
        return builder
            .CreateUser(userId)
            .FundWallet(1000m, "USD")
            .LiveFootballMatch("User Journey Match")
            .PlaceBet(100m, "home_win", 2.10m)
            .WaitForSettlement(2000)
            .CompleteEvent("home_win")
            .WaitForSettlement(1000)
            .VerifyBalance(1110m); // 1000 - 100 + 210
    }
}