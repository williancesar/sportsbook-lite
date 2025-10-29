using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Events.Requests;
using SportsbookLite.Api.Features.Events.Responses;

namespace SportsbookLite.Api.Features.Events.Endpoints;

public sealed class ListEventsEndpoint : Endpoint<ListEventsRequest, EventListResponse>
{
    private readonly IGrainFactory _grainFactory;

    public ListEventsEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Get("/api/events");
        AllowAnonymous();
        Throttle(
            hitLimit: 50,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "List sport events";
            s.Description = "Retrieves a paginated list of sport events with optional filtering";
            s.Response(200, "Events retrieved successfully");
            s.Response(400, "Invalid request parameters");
            s.Response(500, "Retrieval failed");
        });
    }

    public override async Task HandleAsync(ListEventsRequest req, CancellationToken ct)
    {
        try
        {
            Response = new EventListResponse
            {
                IsSuccess = false,
                ErrorMessage = "Event listing not yet implemented - requires event registry grain implementation",
                Events = Array.Empty<EventDto>(),
                TotalCount = 0,
                PageNumber = req.PageNumber,
                PageSize = req.PageSize
            };

            HttpContext.Response.StatusCode = 501;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to list events");
            
            HttpContext.Response.StatusCode = 500;
            Response = new EventListResponse
            {
                IsSuccess = false,
                ErrorMessage = "An error occurred while retrieving events",
                Events = Array.Empty<EventDto>(),
                TotalCount = 0,
                PageNumber = req.PageNumber,
                PageSize = req.PageSize
            };
        }
    }
}