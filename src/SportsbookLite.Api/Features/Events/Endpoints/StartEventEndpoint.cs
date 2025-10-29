using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Events.Responses;
using SportsbookLite.Contracts.Events;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Events.Endpoints;

public sealed class StartEventEndpoint : EndpointWithoutRequest<EventResponse>
{
    private readonly IGrainFactory _grainFactory;

    public StartEventEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Post("/api/events/{eventId}/start");
        AllowAnonymous();
        Throttle(
            hitLimit: 10,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "Start a sport event";
            s.Description = "Starts the specified sport event, changing its status to Live";
            s.Params["eventId"] = "The unique identifier of the event";
            s.Response(200, "Event started successfully");
            s.Response(404, "Event not found");
            s.Response(409, "Event cannot be started in current status");
            s.Response(500, "Start operation failed");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var eventId = Route<Guid>("eventId");
            var eventGrain = _grainFactory.GetGrain<ISportEventGrain>(eventId);
            
            var sportEvent = await eventGrain.StartEventAsync();

            Response = new EventResponse
            {
                IsSuccess = true,
                Event = MapEvent(sportEvent)
            };
        }
        catch (ArgumentException)
        {
            var eventId = Route<Guid>("eventId");
            HttpContext.Response.StatusCode = 404;
            Response = new EventResponse
            {
                IsSuccess = false,
                ErrorMessage = "Event not found"
            };
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.Response.StatusCode = 409;
            Response = new EventResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            var eventId = Route<Guid>("eventId");
            Logger.LogError(ex, "Failed to start event {EventId}", eventId);
            
            HttpContext.Response.StatusCode = 500;
            Response = new EventResponse
            {
                IsSuccess = false,
                ErrorMessage = "An error occurred while starting the event"
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