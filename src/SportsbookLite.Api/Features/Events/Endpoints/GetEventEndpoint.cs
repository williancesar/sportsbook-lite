using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Events.Responses;
using SportsbookLite.Contracts.Events;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Events.Endpoints;

public sealed class GetEventEndpoint : EndpointWithoutRequest<EventResponse>
{
    private readonly IGrainFactory _grainFactory;

    public GetEventEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Get("/api/events/{eventId}");
        AllowAnonymous();
        Throttle(
            hitLimit: 100,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "Get sport event details";
            s.Description = "Retrieves the details of a specific sport event";
            s.Params["eventId"] = "The unique identifier of the event";
            s.Response(200, "Event found");
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
            
            var sportEvent = await eventGrain.GetEventDetailsAsync();

            Response = new EventResponse
            {
                IsSuccess = true,
                Event = MapEvent(sportEvent)
            };
        }
        catch (ArgumentException)
        {
            HttpContext.Response.StatusCode = 404;
            Response = new EventResponse
            {
                IsSuccess = false,
                ErrorMessage = "Event not found"
            };
        }
        catch (Exception ex)
        {
            var eventId = Route<Guid>("eventId");
            Logger.LogError(ex, "Failed to retrieve event {EventId}", eventId);
            
            HttpContext.Response.StatusCode = 500;
            Response = new EventResponse
            {
                IsSuccess = false,
                ErrorMessage = "An error occurred while retrieving the event"
            };
        }
    }

    private static EventDto MapEvent(SportEvent sportEvent) =>
        new()
        {
            Id = sportEvent.Id,
            Name = sportEvent.Name,
            SportType = sportEvent.SportType.ToString(),
            Competition = sportEvent.Competition,
            StartTime = sportEvent.StartTime,
            EndTime = sportEvent.EndTime,
            Status = sportEvent.Status.ToString(),
            Participants = sportEvent.Participants,
            CreatedAt = sportEvent.CreatedAt,
            LastModified = sportEvent.LastModified
        };
}