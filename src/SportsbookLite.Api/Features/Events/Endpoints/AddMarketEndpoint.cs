using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Events.Requests;
using SportsbookLite.Api.Features.Events.Responses;
using SportsbookLite.Contracts.Events;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Events.Endpoints;

public sealed class AddMarketEndpoint : Endpoint<AddMarketRequest, MarketResponse>
{
    private readonly IGrainFactory _grainFactory;

    public AddMarketEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Post("/api/events/{eventId}/markets");
        AllowAnonymous();
        Throttle(
            hitLimit: 20,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "Add a market to an event";
            s.Description = "Adds a new betting market to the specified sport event";
            s.Params["eventId"] = "The unique identifier of the event";
            s.Response(201, "Market created successfully", example: new MarketResponse
            {
                IsSuccess = true,
                Market = new MarketDto
                {
                    Id = Guid.NewGuid(),
                    EventId = Guid.NewGuid(),
                    Name = "Match Winner",
                    Description = "Bet on the winner of the match",
                    Status = "Open",
                    Outcomes = new Dictionary<string, decimal> { ["home"] = 1.85m, ["away"] = 2.10m },
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastModified = DateTimeOffset.UtcNow
                }
            });
            s.Response(400, "Invalid request");
            s.Response(404, "Event not found");
            s.Response(409, "Cannot add market to event in current status");
            s.Response(500, "Market creation failed");
        });
    }

    public override async Task HandleAsync(AddMarketRequest req, CancellationToken ct)
    {
        try
        {
            var eventGrain = _grainFactory.GetGrain<ISportEventGrain>(req.EventId);
            
            var market = await eventGrain.AddMarketAsync(
                req.Name,
                req.Description,
                req.Outcomes);

            HttpContext.Response.StatusCode = 201;
            Response = new MarketResponse
            {
                IsSuccess = true,
                Market = MapMarket(market)
            };
        }
        catch (ArgumentException ex)
        {
            HttpContext.Response.StatusCode = 400;
            Response = new MarketResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.Response.StatusCode = 409;
            Response = new MarketResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to add market to event {EventId}", req.EventId);
            
            HttpContext.Response.StatusCode = 500;
            Response = new MarketResponse
            {
                IsSuccess = false,
                ErrorMessage = "An error occurred while adding the market"
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