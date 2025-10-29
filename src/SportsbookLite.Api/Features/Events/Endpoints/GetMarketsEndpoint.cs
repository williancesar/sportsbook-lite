using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Events.Responses;
using SportsbookLite.Contracts.Events;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Events.Endpoints;

public sealed class GetMarketsEndpoint : EndpointWithoutRequest<MarketsListResponse>
{
    private readonly IGrainFactory _grainFactory;

    public GetMarketsEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Get("/api/events/{eventId}/markets");
        AllowAnonymous();
        Throttle(
            hitLimit: 100,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "Get event markets";
            s.Description = "Retrieves all betting markets for the specified sport event";
            s.Params["eventId"] = "The unique identifier of the event";
            s.Response(200, "Markets retrieved successfully");
            s.Response(404, "Event not found");
            s.Response(500, "Retrieval failed");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var eventId = Route<Guid>("eventId");
            var eventGrain = _grainFactory.GetGrain<ISportEventGrain>(eventId);
            
            var markets = await eventGrain.GetMarketsAsync();

            Response = new MarketsListResponse
            {
                IsSuccess = true,
                Markets = markets.Select(MapMarket).ToList()
            };
        }
        catch (ArgumentException)
        {
            var eventId = Route<Guid>("eventId");
            HttpContext.Response.StatusCode = 404;
            Response = new MarketsListResponse
            {
                IsSuccess = false,
                ErrorMessage = "Event not found",
                Markets = Array.Empty<MarketDto>()
            };
        }
        catch (Exception ex)
        {
            var eventId = Route<Guid>("eventId");
            Logger.LogError(ex, "Failed to retrieve markets for event {EventId}", eventId);
            
            HttpContext.Response.StatusCode = 500;
            Response = new MarketsListResponse
            {
                IsSuccess = false,
                ErrorMessage = "An error occurred while retrieving markets",
                Markets = Array.Empty<MarketDto>()
            };
        }
    }

    private static MarketDto MapMarket(Market market) =>
        new()
        {
            Id = market.Id,
            EventId = market.EventId,
            Name = market.Name,
            Description = market.Description,
            Status = market.Status.ToString(),
            Outcomes = market.Outcomes,
            CreatedAt = market.CreatedAt,
            LastModified = market.LastModified,
            WinningOutcome = market.WinningOutcome
        };
}