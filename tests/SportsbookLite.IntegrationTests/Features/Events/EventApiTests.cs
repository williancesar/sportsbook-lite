using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.TestingHost;
using SportsbookLite.Api;
using SportsbookLite.Api.Features.Events.Requests;
using SportsbookLite.Api.Features.Events.Responses;
using SportsbookLite.Contracts.Events;
using SportsbookLite.GrainInterfaces;
using SportsbookLite.Grains.Events;
using SportsbookLite.TestUtilities;
using System.Net;
using System.Net.Http.Json;

namespace SportsbookLite.IntegrationTests.Features.Events;

public class EventApiTests : BaseIntegrationTest
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
    public async Task CreateEventEndpoint_WithValidRequest_ShouldReturnSuccess()
    {
        var request = new CreateEventRequest
        {
            Name = "Liverpool vs Arsenal",
            SportType = SportType.Soccer,
            Competition = "Premier League",
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            Participants = new Dictionary<string, string>
            {
                { "home", "Liverpool" },
                { "away", "Arsenal" }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/events", request);
        var result = await response.Content.ReadFromJsonAsync<EventResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Event.Should().NotBeNull();
        result.Event!.Id.Should().NotBeEmpty();
        result.Event.Name.Should().Be(request.Name);
        result.Event.SportType.Should().Be(request.SportType.ToString());
        result.Event.Competition.Should().Be(request.Competition);
        result.Event.Status.Should().Be("Scheduled");
        result.Event.Participants.Should().BeEquivalentTo(request.Participants);
        result.Event.StartTime.Should().BeCloseTo(request.StartTime, TimeSpan.FromSeconds(1));
        result.Event.EndTime.Should().BeNull();
    }

    [Fact]
    public async Task CreateEventEndpoint_WithInvalidName_ShouldReturnBadRequest()
    {
        var request = new CreateEventRequest
        {
            Name = "",
            SportType = SportType.Soccer,
            Competition = "Premier League",
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            Participants = new Dictionary<string, string> { { "home", "Team A" } }
        };

        var response = await _client.PostAsJsonAsync("/api/events", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEventEndpoint_WithPastStartTime_ShouldReturnBadRequest()
    {
        var request = new CreateEventRequest
        {
            Name = "Past Event",
            SportType = SportType.Soccer,
            Competition = "Premier League",
            StartTime = DateTimeOffset.UtcNow.AddDays(-1),
            Participants = new Dictionary<string, string> { { "home", "Team A" }, { "away", "Team B" } }
        };

        var response = await _client.PostAsJsonAsync("/api/events", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEventEndpoint_WithEmptyParticipants_ShouldReturnBadRequest()
    {
        var request = new CreateEventRequest
        {
            Name = "Test Event",
            SportType = SportType.Soccer,
            Competition = "Premier League",
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            Participants = new Dictionary<string, string>()
        };

        var response = await _client.PostAsJsonAsync("/api/events", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetEventEndpoint_WithExistingEvent_ShouldReturnSuccess()
    {
        var eventId = await CreateTestEventAsync("Test Event");

        var response = await _client.GetAsync($"/api/events/{eventId}");
        var result = await response.Content.ReadFromJsonAsync<EventResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Event.Should().NotBeNull();
        result.Event!.Id.Should().Be(eventId);
        result.Event.Name.Should().Be("Test Event");
        result.Event.Status.Should().Be("Scheduled");
    }

    [Fact]
    public async Task GetEventEndpoint_WithNonExistentEvent_ShouldReturnNotFound()
    {
        var nonExistentEventId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/events/{nonExistentEventId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StartEventEndpoint_WithScheduledEvent_ShouldReturnSuccess()
    {
        var eventId = await CreateTestEventAsync("Test Event");

        var response = await _client.PostAsync($"/api/events/{eventId}/start", null);
        var result = await response.Content.ReadFromJsonAsync<EventResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Event.Should().NotBeNull();
        result.Event!.Status.Should().Be("Live");
        result.Event.Id.Should().Be(eventId);
    }

    [Fact]
    public async Task StartEventEndpoint_WithNonExistentEvent_ShouldReturnNotFound()
    {
        var nonExistentEventId = Guid.NewGuid();

        var response = await _client.PostAsync($"/api/events/{nonExistentEventId}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StartEventEndpoint_WithAlreadyStartedEvent_ShouldReturnConflict()
    {
        var eventId = await CreateTestEventAsync("Test Event");
        await _client.PostAsync($"/api/events/{eventId}/start", null);

        var response = await _client.PostAsync($"/api/events/{eventId}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CompleteEventEndpoint_WithLiveEvent_ShouldReturnSuccess()
    {
        var eventId = await CreateTestEventAsync("Test Event");
        await _client.PostAsync($"/api/events/{eventId}/start", null);

        var response = await _client.PostAsync($"/api/events/{eventId}/complete", null);
        var result = await response.Content.ReadFromJsonAsync<EventResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Event.Should().NotBeNull();
        result.Event!.Status.Should().Be("Completed");
        result.Event.EndTime.Should().NotBeNull();
        result.Event.EndTime.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CompleteEventEndpoint_WithScheduledEvent_ShouldReturnConflict()
    {
        var eventId = await CreateTestEventAsync("Test Event");

        var response = await _client.PostAsync($"/api/events/{eventId}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CancelEventEndpoint_WithScheduledEvent_ShouldReturnSuccess()
    {
        var eventId = await CreateTestEventAsync("Test Event");
        var request = new CancelEventRequest { Reason = "Weather conditions" };

        var response = await _client.PostAsJsonAsync($"/api/events/{eventId}/cancel", request);
        var result = await response.Content.ReadFromJsonAsync<EventResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Event.Should().NotBeNull();
        result.Event!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task CancelEventEndpoint_WithCompletedEvent_ShouldReturnConflict()
    {
        var eventId = await CreateTestEventAsync("Test Event");
        await _client.PostAsync($"/api/events/{eventId}/start", null);
        await _client.PostAsync($"/api/events/{eventId}/complete", null);

        var request = new CancelEventRequest { Reason = "Test cancellation" };
        var response = await _client.PostAsJsonAsync($"/api/events/{eventId}/cancel", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateEventEndpoint_WithScheduledEvent_ShouldReturnSuccess()
    {
        var eventId = await CreateTestEventAsync("Test Event");
        var updateRequest = new UpdateEventRequest
        {
            Name = "Updated Test Event",
            StartTime = DateTimeOffset.UtcNow.AddDays(2),
            Participants = new Dictionary<string, string>
            {
                { "home", "Updated Team A" },
                { "away", "Updated Team B" }
            }
        };

        var response = await _client.PutAsJsonAsync($"/api/events/{eventId}", updateRequest);
        var result = await response.Content.ReadFromJsonAsync<EventResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Event.Should().NotBeNull();
        result.Event!.Name.Should().Be("Updated Test Event");
        result.Event.Participants.Should().BeEquivalentTo(updateRequest.Participants);
        result.Event.StartTime.Should().BeCloseTo(updateRequest.StartTime!.Value, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UpdateEventEndpoint_WithLiveEvent_ShouldReturnConflict()
    {
        var eventId = await CreateTestEventAsync("Test Event");
        await _client.PostAsync($"/api/events/{eventId}/start", null);

        var updateRequest = new UpdateEventRequest { Name = "Updated Name" };
        var response = await _client.PutAsJsonAsync($"/api/events/{eventId}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddMarketEndpoint_WithValidData_ShouldReturnSuccess()
    {
        var eventId = await CreateTestEventAsync("Test Event");
        var request = new AddMarketRequest
        {
            EventId = eventId,
            Name = "Match Winner",
            Description = "Select the winner of the match",
            Outcomes = new Dictionary<string, decimal>
            {
                { "home", 1.85m },
                { "away", 2.10m },
                { "draw", 3.40m }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/markets", request);
        var result = await response.Content.ReadFromJsonAsync<MarketResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Market.Should().NotBeNull();
        result.Market!.Id.Should().NotBeEmpty();
        result.Market.EventId.Should().Be(eventId);
        result.Market.Name.Should().Be(request.Name);
        result.Market.Description.Should().Be(request.Description);
        result.Market.Status.Should().Be("Open");
        result.Market.Outcomes.Should().BeEquivalentTo(request.Outcomes);
    }

    [Fact]
    public async Task AddMarketEndpoint_WithNonExistentEvent_ShouldReturnBadRequest()
    {
        var nonExistentEventId = Guid.NewGuid();
        var request = new AddMarketRequest
        {
            EventId = nonExistentEventId,
            Name = "Test Market",
            Description = "Test description",
            Outcomes = new Dictionary<string, decimal> { { "home", 1.85m } }
        };

        var response = await _client.PostAsJsonAsync("/api/markets", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMarketsEndpoint_WithExistingEvent_ShouldReturnMarkets()
    {
        var eventId = await CreateTestEventAsync("Test Event");
        await CreateTestMarketAsync(eventId, "Market 1");
        await CreateTestMarketAsync(eventId, "Market 2");

        var response = await _client.GetAsync($"/api/events/{eventId}/markets");
        var result = await response.Content.ReadFromJsonAsync<MarketsListResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Markets.Should().HaveCount(2);
        result.Markets.Select(m => m.Name).Should().Contain(new[] { "Market 1", "Market 2" });
        result.Markets.All(m => m.EventId == eventId).Should().BeTrue();
    }

    [Fact]
    public async Task GetMarketsEndpoint_WithNonExistentEvent_ShouldReturnNotFound()
    {
        var nonExistentEventId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/events/{nonExistentEventId}/markets");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EventLifecycleFlow_ShouldWorkEndToEnd()
    {
        var eventId = await CreateTestEventAsync("Lifecycle Test Event");

        var getResponse = await _client.GetAsync($"/api/events/{eventId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var eventData = await getResponse.Content.ReadFromJsonAsync<EventResponse>();
        eventData!.Event!.Status.Should().Be("Scheduled");

        var marketId = await CreateTestMarketAsync(eventId, "Test Market");

        var startResponse = await _client.PostAsync($"/api/events/{eventId}/start", null);
        startResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var startedEvent = await startResponse.Content.ReadFromJsonAsync<EventResponse>();
        startedEvent!.Event!.Status.Should().Be("Live");

        var completeResponse = await _client.PostAsync($"/api/events/{eventId}/complete", null);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var completedEvent = await completeResponse.Content.ReadFromJsonAsync<EventResponse>();
        completedEvent!.Event!.Status.Should().Be("Completed");
        completedEvent.Event.EndTime.Should().NotBeNull();

        var finalGetResponse = await _client.GetAsync($"/api/events/{eventId}");
        var finalEvent = await finalGetResponse.Content.ReadFromJsonAsync<EventResponse>();
        finalEvent!.Event!.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task ConcurrentEventCreation_ShouldSucceed()
    {
        var tasks = new List<Task<HttpResponseMessage>>();
        
        for (int i = 0; i < 5; i++)
        {
            var request = new CreateEventRequest
            {
                Name = $"Concurrent Event {i}",
                SportType = SportType.Soccer,
                Competition = "Test League",
                StartTime = DateTimeOffset.UtcNow.AddDays(1),
                Participants = new Dictionary<string, string> { { "home", $"Team A{i}" }, { "away", $"Team B{i}" } }
            };
            tasks.Add(_client.PostAsJsonAsync("/api/events", request));
        }

        var responses = await Task.WhenAll(tasks);

        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.Created));
        
        var results = new List<EventResponse>();
        foreach (var response in responses)
        {
            var result = await response.Content.ReadFromJsonAsync<EventResponse>();
            results.Add(result!);
        }

        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
        results.Select(r => r.Event!.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ValidationErrors_ShouldReturnBadRequestWithDetails()
    {
        var request = new CreateEventRequest
        {
            Name = new string('A', 201),
            SportType = SportType.Soccer,
            Competition = "",
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            Participants = new Dictionary<string, string>()
        };

        var response = await _client.PostAsJsonAsync("/api/events", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(SportType.Soccer)]
    [InlineData(SportType.Basketball)]
    [InlineData(SportType.Tennis)]
    [InlineData(SportType.Football)]
    public async Task CreateEventEndpoint_WithDifferentSportTypes_ShouldSucceed(SportType sportType)
    {
        var request = new CreateEventRequest
        {
            Name = $"Test {sportType} Event",
            SportType = sportType,
            Competition = "Test League",
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            Participants = new Dictionary<string, string> { { "home", "Team A" }, { "away", "Team B" } }
        };

        var response = await _client.PostAsJsonAsync("/api/events", request);
        var result = await response.Content.ReadFromJsonAsync<EventResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result!.Event!.SportType.Should().Be(sportType.ToString());
    }

    private async Task<Guid> CreateTestEventAsync(string name = "Test Event")
    {
        var request = new CreateEventRequest
        {
            Name = name,
            SportType = SportType.Soccer,
            Competition = "Test League",
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            Participants = new Dictionary<string, string>
            {
                { "home", "Team A" },
                { "away", "Team B" }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/events", request);
        var result = await response.Content.ReadFromJsonAsync<EventResponse>();
        return result!.Event!.Id;
    }

    private async Task<Guid> CreateTestMarketAsync(Guid eventId, string name = "Test Market")
    {
        var request = new AddMarketRequest
        {
            EventId = eventId,
            Name = name,
            Description = "Test market description",
            Outcomes = new Dictionary<string, decimal>
            {
                { "home", 1.85m },
                { "away", 2.10m }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/markets", request);
        var result = await response.Content.ReadFromJsonAsync<MarketResponse>();
        return result!.Market!.Id;
    }
}