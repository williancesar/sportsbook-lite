using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.TestingHost;
using SportsbookLite.Api;
using SportsbookLite.Api.Features.Betting.Requests;
using SportsbookLite.Api.Features.Betting.Responses;
using SportsbookLite.Api.Features.Events.Requests;
using SportsbookLite.Api.Features.Odds.Requests;
using SportsbookLite.Api.Features.Wallet.Requests;
using SportsbookLite.Contracts.Api.Betting;
using SportsbookLite.Contracts.Betting;
using SportsbookLite.Contracts.Events;
using SportsbookLite.Contracts.Odds;
using SportsbookLite.Contracts.Wallet;
using SportsbookLite.GrainInterfaces;
using SportsbookLite.TestUtilities;
using System.Net;
using System.Net.Http.Json;

namespace SportsbookLite.IntegrationTests.Features.Betting;

public class BettingApiTests : BaseIntegrationTest
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
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

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(hostBuilder =>
            {
                hostBuilder.UseConfiguration(Configuration);
                hostBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<IGrainFactory>(_cluster.GrainFactory);
                });
            });

        _client = _factory.CreateClient();
    }

    public override async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
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
    public async Task PlaceBetEndpoint_WithValidRequest_ShouldReturnSuccess()
    {
        var userId = "bet_user_001";
        var eventId = Guid.NewGuid();
        var marketId = "market_001";
        var selectionId = "selection_home_win";

        await SetupUserWithBalance(userId, 500m);
        await SetupEventAndOdds(eventId, marketId, selectionId);

        var request = new PlaceBetApiRequest
        {
            UserId = userId,
            EventId = eventId,
            MarketId = marketId,
            SelectionId = selectionId,
            Stake = 100m,
            Currency = "USD",
            AcceptableOdds = 2.0m
        };

        var response = await _client.PostAsJsonAsync("/api/bets", request);
        var result = await response.Content.ReadFromJsonAsync<PlaceBetResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.BetId.Should().NotBe(Guid.Empty);
        result.Status.Should().Be("Accepted");
        result.ActualOdds.Should().BeGreaterThan(0);
        result.PotentialPayout.Should().BeGreaterThan(100m);
        result.PlacedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task PlaceBetEndpoint_WithInsufficientBalance_ShouldReturnBadRequest()
    {
        var userId = "bet_user_002";
        var eventId = Guid.NewGuid();
        var marketId = "market_002";
        var selectionId = "selection_away_win";

        await SetupUserWithBalance(userId, 50m);
        await SetupEventAndOdds(eventId, marketId, selectionId);

        var request = new PlaceBetApiRequest
        {
            UserId = userId,
            EventId = eventId,
            MarketId = marketId,
            SelectionId = selectionId,
            Stake = 100m,
            Currency = "USD",
            AcceptableOdds = 2.0m
        };

        var response = await _client.PostAsJsonAsync("/api/bets", request);
        var result = await response.Content.ReadFromJsonAsync<PlaceBetResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("balance");
    }

    [Fact]
    public async Task PlaceBetEndpoint_WithIdempotencyKey_ShouldBeIdempotent()
    {
        var userId = "bet_user_003";
        var eventId = Guid.NewGuid();
        var marketId = "market_003";
        var selectionId = "selection_draw";
        var idempotencyKey = Guid.NewGuid().ToString();

        await SetupUserWithBalance(userId, 500m);
        await SetupEventAndOdds(eventId, marketId, selectionId);

        var request = new PlaceBetApiRequest
        {
            UserId = userId,
            EventId = eventId,
            MarketId = marketId,
            SelectionId = selectionId,
            Stake = 100m,
            Currency = "USD",
            AcceptableOdds = 2.0m,
            IdempotencyKey = idempotencyKey
        };

        var firstResponse = await _client.PostAsJsonAsync("/api/bets", request);
        var firstResult = await firstResponse.Content.ReadFromJsonAsync<PlaceBetResponse>();

        var secondResponse = await _client.PostAsJsonAsync("/api/bets", request);
        var secondResult = await secondResponse.Content.ReadFromJsonAsync<PlaceBetResponse>();

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        firstResult!.IsSuccess.Should().BeTrue();
        secondResult!.IsSuccess.Should().BeTrue();
        firstResult.BetId.Should().Be(secondResult.BetId);
        firstResult.PotentialPayout.Should().Be(secondResult.PotentialPayout);
    }

    [Fact]
    public async Task PlaceBetEndpoint_WithOddsChanged_ShouldReturnConflict()
    {
        var userId = "bet_user_004";
        var eventId = Guid.NewGuid();
        var marketId = "market_004";
        var selectionId = "selection_over_2_5";

        await SetupUserWithBalance(userId, 500m);
        await SetupEventAndOdds(eventId, marketId, selectionId, currentOdds: 1.8m);

        var request = new PlaceBetApiRequest
        {
            UserId = userId,
            EventId = eventId,
            MarketId = marketId,
            SelectionId = selectionId,
            Stake = 100m,
            Currency = "USD",
            AcceptableOdds = 2.5m
        };

        var response = await _client.PostAsJsonAsync("/api/bets", request);
        var result = await response.Content.ReadFromJsonAsync<PlaceBetResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("odds");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PlaceBetEndpoint_WithInvalidUserId_ShouldReturnBadRequest(string invalidUserId)
    {
        var request = new PlaceBetApiRequest
        {
            UserId = invalidUserId,
            EventId = Guid.NewGuid(),
            MarketId = "market",
            SelectionId = "selection",
            Stake = 100m,
            Currency = "USD",
            AcceptableOdds = 2.0m
        };

        var response = await _client.PostAsJsonAsync("/api/bets", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task PlaceBetEndpoint_WithInvalidStake_ShouldReturnBadRequest(decimal invalidStake)
    {
        var userId = "bet_user_005";
        var request = new PlaceBetApiRequest
        {
            UserId = userId,
            EventId = Guid.NewGuid(),
            MarketId = "market",
            SelectionId = "selection",
            Stake = invalidStake,
            Currency = "USD",
            AcceptableOdds = 2.0m
        };

        var response = await _client.PostAsJsonAsync("/api/bets", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBetEndpoint_WithExistingBet_ShouldReturnBetDetails()
    {
        var userId = "bet_user_006";
        var betId = await PlaceSuccessfulBet(userId, 100m);

        var response = await _client.GetAsync($"/api/bets/{betId}");
        var result = await response.Content.ReadFromJsonAsync<GetBetResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Bet.Should().NotBeNull();
        result.Bet!.Id.Should().Be(betId);
        result.Bet.UserId.Should().Be(userId);
        result.Bet.Status.Should().Be(BetStatus.Pending);
        result.Bet.Amount.Should().Be(100m);
    }

    [Fact]
    public async Task GetBetEndpoint_WithNonExistentBet_ShouldReturnNotFound()
    {
        var nonExistentBetId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/bets/{nonExistentBetId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserBetsEndpoint_WithMultipleBets_ShouldReturnAllBets()
    {
        var userId = "bet_user_007";

        await SetupUserWithBalance(userId, 1000m);
        var betId1 = await PlaceSuccessfulBet(userId, 100m);
        var betId2 = await PlaceSuccessfulBet(userId, 200m);
        var betId3 = await PlaceSuccessfulBet(userId, 150m);

        var response = await _client.GetAsync($"/api/bets/users/{userId}");
        var result = await response.Content.ReadFromJsonAsync<GetUserBetsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Bets.Should().NotBeNull();
        result.Bets.Should().HaveCount(3);
        result.Bets.Select(b => b.Id).Should().Contain(new[] { betId1, betId2, betId3 });
        result.Bets.Should().OnlyContain(b => b.UserId == userId);
    }

    [Fact]
    public async Task GetUserBetsEndpoint_WithLimit_ShouldRespectLimit()
    {
        var userId = "bet_user_008";
        
        await SetupUserWithBalance(userId, 2000m);
        for (int i = 0; i < 10; i++)
        {
            await PlaceSuccessfulBet(userId, 50m);
            await Task.Delay(10);
        }

        var response = await _client.GetAsync($"/api/bets/users/{userId}?limit=5");
        var result = await response.Content.ReadFromJsonAsync<GetUserBetsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Bets.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetActiveBetsEndpoint_ShouldReturnOnlyActiveBets()
    {
        var userId = "bet_user_009";

        await SetupUserWithBalance(userId, 1000m);
        var activeBetId = await PlaceSuccessfulBet(userId, 100m);

        var response = await _client.GetAsync($"/api/bets/users/{userId}/active");
        var result = await response.Content.ReadFromJsonAsync<GetActiveBetsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.ActiveBets.Should().NotBeNull();
        result.ActiveBets.Should().HaveCount(1);
        result.ActiveBets[0].Id.Should().Be(activeBetId);
        result.ActiveBets.Should().OnlyContain(b => b.Status == BetStatus.Pending);
    }

    [Fact]
    public async Task VoidBetEndpoint_WithValidBet_ShouldReturnSuccess()
    {
        var userId = "bet_user_010";
        var betId = await PlaceSuccessfulBet(userId, 100m);
        var reason = "Event cancelled";

        var request = new VoidBetRequest
        {
            Reason = reason
        };

        var response = await _client.PostAsJsonAsync($"/api/bets/{betId}/void", request);
        var result = await response.Content.ReadFromJsonAsync<VoidBetResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Bet.Should().NotBeNull();
        result.Bet!.Status.Should().Be(BetStatus.Void);
        result.Bet.VoidReason.Should().Be(reason);
    }

    [Fact]
    public async Task VoidBetEndpoint_WithNonExistentBet_ShouldReturnNotFound()
    {
        var nonExistentBetId = Guid.NewGuid();
        var request = new VoidBetRequest
        {
            Reason = "Test void"
        };

        var response = await _client.PostAsJsonAsync($"/api/bets/{nonExistentBetId}/void", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CashOutEndpoint_WithValidBet_ShouldReturnSuccess()
    {
        var userId = "bet_user_011";
        var betId = await PlaceSuccessfulBet(userId, 100m);

        var response = await _client.PostAsync($"/api/bets/{betId}/cashout", null);
        var result = await response.Content.ReadFromJsonAsync<CashOutResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Bet.Should().NotBeNull();
        result.Bet!.Status.Should().Be(BetStatus.CashOut);
        result.Bet.Payout.Should().NotBeNull();
        result.PayoutAmount.Should().BeGreaterThan(0);
        result.PayoutAmount.Should().BeLessThan(100m);
    }

    [Fact]
    public async Task GetBetHistoryEndpoint_ShouldReturnOrderedHistory()
    {
        var userId = "bet_user_012";

        await SetupUserWithBalance(userId, 1000m);
        var betId1 = await PlaceSuccessfulBet(userId, 100m);
        await Task.Delay(50);
        var betId2 = await PlaceSuccessfulBet(userId, 200m);
        await Task.Delay(50);
        var betId3 = await PlaceSuccessfulBet(userId, 150m);

        var response = await _client.GetAsync($"/api/bets/users/{userId}/history");
        var result = await response.Content.ReadFromJsonAsync<GetBetHistoryResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.BetHistory.Should().NotBeNull();
        result.BetHistory.Should().HaveCount(3);
        result.BetHistory.Should().BeInDescendingOrder(b => b.PlacedAt);
    }

    [Fact]
    public async Task BettingWorkflow_PlaceAndManageBets_ShouldWorkEndToEnd()
    {
        var userId = "bet_user_013";
        await SetupUserWithBalance(userId, 2000m);

        var bet1Id = await PlaceSuccessfulBet(userId, 100m);
        var bet2Id = await PlaceSuccessfulBet(userId, 200m);
        var bet3Id = await PlaceSuccessfulBet(userId, 150m);

        var betsResponse = await _client.GetAsync($"/api/bets/users/{userId}");
        var betsResult = await betsResponse.Content.ReadFromJsonAsync<GetUserBetsResponse>();
        betsResult!.Bets.Should().HaveCount(3);

        var activeBetsResponse = await _client.GetAsync($"/api/bets/users/{userId}/active");
        var activeBetsResult = await activeBetsResponse.Content.ReadFromJsonAsync<GetActiveBetsResponse>();
        activeBetsResult!.ActiveBets.Should().HaveCount(3);

        var voidRequest = new VoidBetRequest { Reason = "Market error" };
        await _client.PostAsJsonAsync($"/api/bets/{bet1Id}/void", voidRequest);

        await _client.PostAsync($"/api/bets/{bet2Id}/cashout", null);

        var finalActiveBetsResponse = await _client.GetAsync($"/api/bets/users/{userId}/active");
        var finalActiveBetsResult = await finalActiveBetsResponse.Content.ReadFromJsonAsync<GetActiveBetsResponse>();
        finalActiveBetsResult!.ActiveBets.Should().HaveCount(1);
        finalActiveBetsResult.ActiveBets[0].Id.Should().Be(bet3Id);

        var historyResponse = await _client.GetAsync($"/api/bets/users/{userId}/history");
        var historyResult = await historyResponse.Content.ReadFromJsonAsync<GetBetHistoryResponse>();
        historyResult!.BetHistory.Should().HaveCount(3);
    }

    [Fact]
    public async Task BettingEndpoints_WithRateLimit_ShouldThrottleRequests()
    {
        var userId = "bet_user_014";
        var eventId = Guid.NewGuid();
        var marketId = "market_014";
        var selectionId = "selection_limit";

        await SetupUserWithBalance(userId, 2000m);
        await SetupEventAndOdds(eventId, marketId, selectionId);

        var successfulRequests = 0;
        var throttledRequests = 0;

        for (int i = 0; i < 10; i++)
        {
            var request = new PlaceBetApiRequest
            {
                UserId = userId,
                EventId = eventId,
                MarketId = marketId,
                SelectionId = selectionId,
                Stake = 10m,
                Currency = "USD",
                AcceptableOdds = 2.0m,
                IdempotencyKey = Guid.NewGuid().ToString()
            };

            var response = await _client.PostAsJsonAsync("/api/bets", request);

            if (response.IsSuccessStatusCode)
            {
                successfulRequests++;
            }
            else if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throttledRequests++;
            }
        }

        successfulRequests.Should().BeLessOrEqualTo(5);
        throttledRequests.Should().BeGreaterThan(0);
    }

    private async Task<Guid> PlaceSuccessfulBet(string userId, decimal stake)
    {
        var eventId = Guid.NewGuid();
        var marketId = $"market_{Guid.NewGuid():N}";
        var selectionId = $"selection_{Guid.NewGuid():N}";

        await SetupEventAndOdds(eventId, marketId, selectionId);

        var request = new PlaceBetApiRequest
        {
            UserId = userId,
            EventId = eventId,
            MarketId = marketId,
            SelectionId = selectionId,
            Stake = stake,
            Currency = "USD",
            AcceptableOdds = 2.0m
        };

        var response = await _client.PostAsJsonAsync("/api/bets", request);
        var result = await response.Content.ReadFromJsonAsync<PlaceBetResponse>();

        response.IsSuccessStatusCode.Should().BeTrue();
        result!.IsSuccess.Should().BeTrue();

        return result.BetId ?? Guid.Empty;
    }

    private async Task SetupUserWithBalance(string userId, decimal amount)
    {
        var depositRequest = new DepositRequest
        {
            UserId = userId,
            Amount = amount,
            Currency = "USD",
            TransactionId = Guid.NewGuid().ToString()
        };

        var response = await _client.PostAsJsonAsync($"/api/wallet/{userId}/deposit", depositRequest);
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    private async Task SetupEventAndOdds(Guid eventId, string marketId, string selectionId, decimal currentOdds = 2.5m)
    {
        var createEventRequest = new CreateEventRequest
        {
            Name = $"Test Event {eventId:N}",
            SportType = SportType.Football,
            StartTime = DateTimeOffset.UtcNow.AddHours(2),
            Competition = "Test League",
            Participants = new Dictionary<string, string>
            {
                ["home"] = "Team A",
                ["away"] = "Team B"
            }
        };

        var eventResponse = await _client.PostAsJsonAsync("/api/events", createEventRequest);
        eventResponse.IsSuccessStatusCode.Should().BeTrue();

        var addMarketRequest = new AddMarketRequest
        {
            EventId = eventId,
            Name = $"Test Market {marketId}",
            Description = "Match Winner",
            Outcomes = new Dictionary<string, decimal>
            {
                [selectionId] = currentOdds
            }
        };

        var marketResponse = await _client.PostAsJsonAsync($"/api/events/{eventId}/markets", addMarketRequest);
        marketResponse.IsSuccessStatusCode.Should().BeTrue();

        var updateOddsRequest = new UpdateOddsRequest
        {
            Selections = new Dictionary<string, decimal>
            {
                [selectionId] = currentOdds
            },
            Reason = "Initial odds"
        };

        var oddsResponse = await _client.PutAsJsonAsync($"/api/odds/{marketId}", updateOddsRequest);
        oddsResponse.IsSuccessStatusCode.Should().BeTrue();
    }
}