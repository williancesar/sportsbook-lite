using SportsbookLite.JourneyTests.Builders.Features;

namespace SportsbookLite.JourneyTests.Journeys;

public sealed class HappyPathJourneyTests : JourneyTestBase
{
    public HappyPathJourneyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task CompleteUserBettingJourney_FromRegistrationToPayout_ShouldSucceed()
    {
        // Arrange & Act
        var context = await new SportsbookJourneyBuilder(ApiClient, Output)
            .CreateUser("happy_path_user")
            .FundWallet(1000m, "USD")
            .CreateEvent("Champions League Final", SportType.Football)
            .AddMarket("match_result", "home_win", "draw", "away_win")
            .SetOdds("home_win", 2.10m)
            .SetOdds("draw", 3.20m)
            .SetOdds("away_win", 3.50m)
            .StartEvent()
            .PlaceBet(100m, "home_win", 2.10m)
            .WaitForSettlement(500)
            .CompleteEvent("home_win")
            .WaitForSettlement(1000)
            .VerifyBetStatus(BetStatus.Won)
            .VerifyBalance(1110m) // 1000 - 100 + 210
            .ExecuteAsync();

        // Assert
        context.AssertAllSuccessful();
        
        var betId = context.Get<Guid>("lastBetId");
        betId.Should().NotBeEmpty();
        
        Output.WriteLine($"Journey completed successfully. Bet {betId} won and paid out.");
    }

    [Fact]
    public async Task MultipleBetsOnSameEvent_DifferentMarkets_ShouldSucceed()
    {
        // Arrange & Act
        var context = await new SportsbookJourneyBuilder(ApiClient, Output)
            .StandardUserWithBalance("multi_bet_user", 2000m)
            .MultiMarketEvent("World Cup Final")
            .StartEvent()
            .PlaceBet(100m, "home_win", 2.10m)
            .Then(ctx => ctx.Set("market_match_result", ctx.Get<string>("lastMarketId")))
            .AddMarket("both_teams_score", "yes", "no")
            .SetOdds("yes", 1.70m)
            .SetOdds("no", 2.10m)
            .PlaceBet(50m, "yes", 1.70m)
            .Then(ctx => ctx.Set("market_btts", ctx.Get<string>("lastMarketId")))
            .VerifyBalance(1850m) // 2000 - 100 - 50
            .CompleteEvent("home_win")
            .WaitForSettlement(1000)
            .ExecuteAsync();

        // Assert
        context.AssertAllSuccessful();
        
        var userBets = context.Get<List<Guid>>("userBets");
        userBets.Should().HaveCount(2);
        userBets.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SequentialBettingAcrossMultipleEvents_ShouldMaintainBalance()
    {
        // Arrange
        var userId = "sequential_bettor";
        var initialBalance = 500m;
        
        // Act
        var builder = new SportsbookJourneyBuilder(ApiClient, Output)
            .CreateUser(userId)
            .FundWallet(initialBalance, "USD");

        // Place bets on three different events
        for (int i = 1; i <= 3; i++)
        {
            builder = builder
                .CreateEvent($"Match {i}", SportType.Football)
                .AddMarket("match_result", "home", "away")
                .SetOdds("home", 2.00m)
                .SetOdds("away", 2.00m)
                .StartEvent()
                .PlaceBet(50m, "home", 2.00m)
                .Then(ctx => ctx.Set($"event_{i}_id", ctx.Get<Guid>("eventId")))
                .Then(ctx => ctx.Set($"bet_{i}_id", ctx.Get<Guid>("lastBetId")));
        }

        // Complete all events with home wins
        for (int i = 1; i <= 3; i++)
        {
            builder = builder
                .Then(ctx => ctx.Set("eventId", ctx.Get<Guid>($"event_{i}_id")))
                .CompleteEvent("home");
        }

        var context = await builder
            .WaitForSettlement(2000)
            .VerifyBalance(650m) // 500 - 150 + 300 (3 * 100 winnings)
            .ExecuteAsync();

        // Assert
        context.AssertAllSuccessful();
        
        for (int i = 1; i <= 3; i++)
        {
            var betId = context.Get<Guid>($"bet_{i}_id");
            betId.Should().NotBeEmpty($"Bet {i} should have been placed");
        }
    }

    [Fact]
    public async Task UserJourneyWithDepositWithdrawCycle_ShouldTrackCorrectly()
    {
        // Arrange & Act
        var context = await new SportsbookJourneyBuilder(ApiClient, Output)
            .CreateUser("deposit_withdraw_user")
            .FundWallet(1000m, "USD")
            .VerifyBalance(1000m)
            .FundWallet(500m, "USD")  // Additional deposit
            .VerifyBalance(1500m)
            .CreateEvent("Test Match", SportType.Tennis)
            .AddMarket("winner", "player_a", "player_b")
            .SetOdds("player_a", 1.50m)
            .SetOdds("player_b", 2.50m)
            .StartEvent()
            .PlaceBet(200m, "player_a", 1.50m)
            .VerifyBalance(1300m)
            .CompleteEvent("player_a")
            .WaitForSettlement(1000)
            .VerifyBalance(1600m) // 1300 + 300 (200 * 1.5)
            .ExecuteAsync();

        // Assert
        context.AssertAllSuccessful();
        
        var finalBalance = 1600m;
        Output.WriteLine($"Final balance after complete cycle: {finalBalance}");
    }

    [Fact]
    public async Task LuckyStreakJourney_MultipleWinningBets_ShouldCompoundWinnings()
    {
        // Arrange
        var startingBalance = 100m;
        var betAmount = 50m;
        
        // Act
        var context = await new SportsbookJourneyBuilder(ApiClient, Output)
            .CreateUser("lucky_player")
            .FundWallet(startingBalance, "USD")
            // First bet
            .SettlementReadyEvent("home_win")
            .PlaceBet(betAmount, "home_win", 2.00m)
            .WaitForSettlement(1000)
            .VerifyBalance(150m) // 100 - 50 + 100
            // Second bet with winnings
            .SettlementReadyEvent("home_win")
            .PlaceBet(100m, "home_win", 2.00m)
            .WaitForSettlement(1000)
            .VerifyBalance(250m) // 150 - 100 + 200
            // Third bet
            .SettlementReadyEvent("home_win")
            .PlaceBet(200m, "home_win", 2.00m)
            .WaitForSettlement(1000)
            .VerifyBalance(450m) // 250 - 200 + 400
            .ExecuteAsync();

        // Assert
        context.AssertAllSuccessful();
        
        var userBets = context.Get<List<Guid>>("userBets");
        userBets.Should().HaveCount(3, "Three winning bets were placed");
        
        Output.WriteLine($"Lucky streak completed! Starting balance: {startingBalance}, Final: 450");
    }
}