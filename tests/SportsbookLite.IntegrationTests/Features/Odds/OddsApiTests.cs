using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.TestingHost;
using SportsbookLite.Api;
using SportsbookLite.Api.Features.Odds.Requests;
using SportsbookLite.Api.Features.Odds.Responses;
using SportsbookLite.Contracts.Odds;
using SportsbookLite.GrainInterfaces;
using SportsbookLite.Grains.Odds;
using SportsbookLite.TestUtilities;
using System.Net;
using System.Net.Http.Json;

namespace SportsbookLite.IntegrationTests.Features.Odds;

public class OddsApiTests : BaseIntegrationTest
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
    public async Task UpdateOddsEndpoint_WithValidRequest_ShouldReturnSuccess()
    {
        var marketId = "api_match_001";
        await InitializeMarketAsync(marketId);

        var request = new UpdateOddsRequest
        {
            Selections = new Dictionary<string, decimal>
            {
                ["Home Win"] = 1.8m,
                ["Draw"] = 3.5m
            },
            Reason = "API test update",
            UpdatedBy = "test_user"
        };

        var response = await _client.PutAsJsonAsync($"/api/odds/{marketId}", request);
        var result = await response.Content.ReadFromJsonAsync<OddsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.MarketId.Should().Be(marketId);
        result.Selections.Should().ContainKey("Home Win");
        result.Selections["Home Win"].Decimal.Should().Be(1.8m);
        result.Selections["Home Win"].Fractional.Should().Be(0.8m);
        result.Selections["Home Win"].American.Should().Be(-125);
        result.Selections.Should().ContainKey("Draw");
        result.Selections["Draw"].Decimal.Should().Be(3.5m);
        result.Volatility.Should().BeOneOf(OddsVolatility.Low, OddsVolatility.Medium);
        result.IsSuspended.Should().BeFalse();
        result.TotalMargin.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateOddsEndpoint_WithInvalidOdds_ShouldReturnBadRequest()
    {
        var marketId = "api_match_002";
        await InitializeMarketAsync(marketId);

        var request = new UpdateOddsRequest
        {
            Selections = new Dictionary<string, decimal>
            {
                ["Home Win"] = 0m
            }
        };

        var response = await _client.PutAsJsonAsync($"/api/odds/{marketId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateOddsEndpoint_WhenMarketSuspended_ShouldReturnConflict()
    {
        var marketId = "api_match_003";
        await InitializeMarketAsync(marketId);
        await SuspendMarketAsync(marketId);

        var request = new UpdateOddsRequest
        {
            Selections = new Dictionary<string, decimal>
            {
                ["Home Win"] = 1.9m
            }
        };

        var response = await _client.PutAsJsonAsync($"/api/odds/{marketId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetOddsEndpoint_WithExistingMarket_ShouldReturnOdds()
    {
        var marketId = "api_match_004";
        await InitializeMarketAsync(marketId);

        var response = await _client.GetAsync($"/api/odds/{marketId}");
        var result = await response.Content.ReadFromJsonAsync<OddsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.MarketId.Should().Be(marketId);
        result.Selections.Should().HaveCount(3);
        result.Selections.Should().ContainKey("Home Win");
        result.Selections.Should().ContainKey("Draw");
        result.Selections.Should().ContainKey("Away Win");
        result.Volatility.Should().Be(OddsVolatility.Low);
        result.IsSuspended.Should().BeFalse();
    }

    [Fact]
    public async Task GetOddsEndpoint_WithFormats_ShouldReturnCorrectConversions()
    {
        var marketId = "api_match_005";
        await InitializeMarketAsync(marketId);

        var response = await _client.GetAsync($"/api/odds/{marketId}?format=all");
        var result = await response.Content.ReadFromJsonAsync<OddsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Selections["Home Win"].Decimal.Should().Be(2.0m);
        result.Selections["Home Win"].Fractional.Should().Be(1.0m);
        result.Selections["Home Win"].American.Should().Be(100);
        result.Selections["Home Win"].ImpliedProbability.Should().BeApproximately(0.5m, 0.01m);
        result.Selections["Draw"].Decimal.Should().Be(3.2m);
        result.Selections["Draw"].Fractional.Should().Be(2.2m);
        result.Selections["Away Win"].Decimal.Should().Be(4.5m);
        result.Selections["Away Win"].American.Should().Be(350);
    }

    [Fact]
    public async Task SuspendOddsEndpoint_WithValidRequest_ShouldSuspendMarket()
    {
        var marketId = "api_match_006";
        await InitializeMarketAsync(marketId);

        var request = new SuspendOddsRequest
        {
            Reason = "API test suspension",
            SuspendedBy = "test_operator"
        };

        var response = await _client.PostAsJsonAsync($"/api/odds/{marketId}/suspend", request);
        var result = await response.Content.ReadFromJsonAsync<OddsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.MarketId.Should().Be(marketId);
        result.IsSuspended.Should().BeTrue();
        result.SuspensionReason.Should().Be("API test suspension");
    }

    [Fact]
    public async Task ResumeOddsEndpoint_WhenSuspended_ShouldResumeMarket()
    {
        var marketId = "api_match_007";
        await InitializeMarketAsync(marketId);
        await SuspendMarketAsync(marketId);

        var request = new ResumeOddsRequest
        {
            Reason = "API test resumption",
            ResumedBy = "test_operator"
        };

        var response = await _client.PostAsJsonAsync($"/api/odds/{marketId}/resume", request);
        var result = await response.Content.ReadFromJsonAsync<OddsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.MarketId.Should().Be(marketId);
        result.IsSuspended.Should().BeFalse();
        result.SuspensionReason.Should().BeNull();
    }

    [Fact]
    public async Task LockOddsEndpoint_WithValidSelection_ShouldLockOdds()
    {
        var marketId = "api_match_008";
        await InitializeMarketAsync(marketId);

        var request = new LockOddsRequest
        {
            BetId = "BET_API_001",
            SelectionId = "Home Win"
        };

        var response = await _client.PostAsJsonAsync($"/api/odds/{marketId}/lock", request);
        var result = await response.Content.ReadFromJsonAsync<LockResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.IsLocked.Should().BeTrue();
        result.SelectionId.Should().Be("Home Win");
        result.BetId.Should().Be("BET_API_001");
    }

    [Fact]
    public async Task UnlockOddsEndpoint_WithValidBetId_ShouldUnlockOdds()
    {
        var marketId = "api_match_009";
        var betId = "BET_API_002";
        await InitializeMarketAsync(marketId);
        await LockSelectionAsync(marketId, betId, "Home Win");

        var request = new UnlockOddsRequest
        {
            BetId = betId
        };

        var response = await _client.PostAsJsonAsync($"/api/odds/{marketId}/unlock", request);
        var result = await response.Content.ReadFromJsonAsync<LockResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.IsLocked.Should().BeFalse();
        result.BetId.Should().Be(betId);
    }

    [Fact]
    public async Task GetVolatilityEndpoint_ShouldReturnVolatilityInformation()
    {
        var marketId = "api_match_010";
        await InitializeMarketAsync(marketId);
        await CreateVolatilityAsync(marketId);

        var response = await _client.GetAsync($"/api/odds/{marketId}/volatility?window=5");
        var result = await response.Content.ReadFromJsonAsync<VolatilityResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.MarketId.Should().Be(marketId);
        result.Level.Should().BeOneOf(OddsVolatility.Low, OddsVolatility.Medium, OddsVolatility.High);
        result.Score.Should().BeGreaterOrEqualTo(0);
        result.Window.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task GetOddsHistoryEndpoint_WithSelection_ShouldReturnHistory()
    {
        var marketId = "api_match_011";
        await InitializeMarketAsync(marketId);
        await UpdateOddsForHistoryAsync(marketId);

        var response = await _client.GetAsync($"/api/odds/{marketId}/history/Home%20Win");
        var result = await response.Content.ReadFromJsonAsync<OddsHistoryResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.MarketId.Should().Be(marketId);
        result.Selection.Should().Be("Home Win");
        result.Updates.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task GetOddsHistoryEndpoint_WithInvalidSelection_ShouldReturnNotFound()
    {
        var marketId = "api_match_012";
        await InitializeMarketAsync(marketId);

        var response = await _client.GetAsync($"/api/odds/{marketId}/history/Invalid%20Selection");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateOddsEndpoint_WithHighVolatility_ShouldAutoSuspend()
    {
        var marketId = "api_match_013";
        await InitializeMarketAsync(marketId);

        for (int i = 0; i < 25; i++)
        {
            var request = new UpdateOddsRequest
            {
                Selections = new Dictionary<string, decimal>
                {
                    ["Home Win"] = 2.0m + (i % 2 == 0 ? 0.6m : -0.5m)
                },
                Reason = $"Volatility test update {i}"
            };

            var response = await _client.PutAsJsonAsync($"/api/odds/{marketId}", request);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                break;
            }
            await Task.Delay(10);
        }

        var finalResponse = await _client.GetAsync($"/api/odds/{marketId}");
        var result = await finalResponse.Content.ReadFromJsonAsync<OddsResponse>();

        result.Should().NotBeNull();
        result!.IsSuspended.Should().BeTrue();
        result.Volatility.Should().Be(OddsVolatility.Extreme);
    }

    [Fact]
    public async Task ConcurrentUpdates_ShouldMaintainConsistency()
    {
        var marketId = "api_match_014";
        await InitializeMarketAsync(marketId);

        var updateTasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 10; i++)
        {
            var request = new UpdateOddsRequest
            {
                Selections = new Dictionary<string, decimal>
                {
                    ["Home Win"] = 2.0m + (i * 0.05m)
                },
                Reason = $"Concurrent update {i}"
            };

            updateTasks.Add(_client.PutAsJsonAsync($"/api/odds/{marketId}", request));
        }

        var responses = await Task.WhenAll(updateTasks);

        var successfulResponses = responses.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();
        successfulResponses.Should().HaveCountGreaterOrEqualTo(5);

        var finalResponse = await _client.GetAsync($"/api/odds/{marketId}");
        var result = await finalResponse.Content.ReadFromJsonAsync<OddsResponse>();

        result.Should().NotBeNull();
        result!.Selections["Home Win"].Decimal.Should().BeGreaterThan(2.0m);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid_market")]
    public async Task GetOddsEndpoint_WithInvalidMarket_ShouldReturnNotFound(string marketId)
    {
        var response = await _client.GetAsync($"/api/odds/{marketId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateOddsEndpoint_WithEmptySelections_ShouldReturnBadRequest()
    {
        var marketId = "api_match_015";
        await InitializeMarketAsync(marketId);

        var request = new UpdateOddsRequest
        {
            Selections = new Dictionary<string, decimal>()
        };

        var response = await _client.PutAsJsonAsync($"/api/odds/{marketId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LockOddsEndpoint_WhenMarketSuspended_ShouldReturnConflict()
    {
        var marketId = "api_match_016";
        await InitializeMarketAsync(marketId);
        await SuspendMarketAsync(marketId);

        var request = new LockOddsRequest
        {
            BetId = "BET_API_003",
            SelectionId = "Home Win"
        };

        var response = await _client.PostAsJsonAsync($"/api/odds/{marketId}/lock", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(0)]
    [InlineData(100.01)]
    public async Task UpdateOddsEndpoint_WithInvalidOddsValues_ShouldReturnBadRequest(decimal invalidOdds)
    {
        var marketId = "api_match_017";
        await InitializeMarketAsync(marketId);

        var request = new UpdateOddsRequest
        {
            Selections = new Dictionary<string, decimal>
            {
                ["Home Win"] = invalidOdds
            }
        };

        var response = await _client.PutAsJsonAsync($"/api/odds/{marketId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OddsEndpoints_EndToEnd_ShouldWorkCorrectly()
    {
        var marketId = "api_match_e2e";
        await InitializeMarketAsync(marketId);

        var getInitialResponse = await _client.GetAsync($"/api/odds/{marketId}");
        var initialResult = await getInitialResponse.Content.ReadFromJsonAsync<OddsResponse>();
        initialResult!.Selections["Home Win"].Decimal.Should().Be(2.0m);

        var updateRequest = new UpdateOddsRequest
        {
            Selections = new Dictionary<string, decimal>
            {
                ["Home Win"] = 1.85m,
                ["Draw"] = 3.4m
            },
            Reason = "E2E test update",
            UpdatedBy = "test_user"
        };
        var updateResponse = await _client.PutAsJsonAsync($"/api/odds/{marketId}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var lockRequest = new LockOddsRequest
        {
            BetId = "BET_E2E_001",
            SelectionId = "Home Win"
        };
        var lockResponse = await _client.PostAsJsonAsync($"/api/odds/{marketId}/lock", lockRequest);
        lockResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var suspendRequest = new SuspendOddsRequest
        {
            Reason = "E2E test suspension",
            SuspendedBy = "test_operator"
        };
        var suspendResponse = await _client.PostAsJsonAsync($"/api/odds/{marketId}/suspend", suspendRequest);
        suspendResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var resumeRequest = new ResumeOddsRequest
        {
            Reason = "E2E test resumption",
            ResumedBy = "test_operator"
        };
        var resumeResponse = await _client.PostAsJsonAsync($"/api/odds/{marketId}/resume", resumeRequest);
        resumeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var unlockRequest = new UnlockOddsRequest
        {
            BetId = "BET_E2E_001"
        };
        var unlockResponse = await _client.PostAsJsonAsync($"/api/odds/{marketId}/unlock", unlockRequest);
        unlockResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var finalResponse = await _client.GetAsync($"/api/odds/{marketId}");
        var finalResult = await finalResponse.Content.ReadFromJsonAsync<OddsResponse>();
        finalResult!.Selections["Home Win"].Decimal.Should().Be(1.85m);
        finalResult.IsSuspended.Should().BeFalse();
    }

    private async Task InitializeMarketAsync(string marketId)
    {
        var grain = _cluster.GrainFactory.GetGrain<IOddsGrain>(marketId);
        var initialOdds = new Dictionary<string, decimal>
        {
            ["Home Win"] = 2.0m,
            ["Draw"] = 3.2m,
            ["Away Win"] = 4.5m
        };
        await grain.InitializeMarketAsync(initialOdds);
    }

    private async Task SuspendMarketAsync(string marketId)
    {
        var grain = _cluster.GrainFactory.GetGrain<IOddsGrain>(marketId);
        await grain.SuspendOddsAsync("Test suspension");
    }

    private async Task LockSelectionAsync(string marketId, string betId, string selection)
    {
        var grain = _cluster.GrainFactory.GetGrain<IOddsGrain>(marketId);
        await grain.LockOddsForBetAsync(betId, selection);
    }

    private async Task UpdateOddsForHistoryAsync(string marketId)
    {
        var grain = _cluster.GrainFactory.GetGrain<IOddsGrain>(marketId);
        var updateRequest = OddsUpdateRequest.Create(
            marketId,
            new Dictionary<string, decimal> { ["Home Win"] = 1.9m },
            OddsSource.Feed,
            "History test update");
        await grain.UpdateOddsAsync(updateRequest);
    }

    private async Task CreateVolatilityAsync(string marketId)
    {
        var grain = _cluster.GrainFactory.GetGrain<IOddsGrain>(marketId);
        
        for (int i = 0; i < 3; i++)
        {
            var updateRequest = OddsUpdateRequest.Create(
                marketId,
                new Dictionary<string, decimal> { ["Home Win"] = 2.0m + (i * 0.1m) },
                OddsSource.Feed,
                $"Volatility test {i}");
            await grain.UpdateOddsAsync(updateRequest);
            await Task.Delay(10);
        }
    }
}