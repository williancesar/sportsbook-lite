using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Events.Requests;
using SportsbookLite.Api.Features.Events.Responses;
using SportsbookLite.Contracts.Events;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Events.Endpoints;

public sealed class CancelEventEndpoint : Endpoint<CancelEventRequest, EventResponse>
{
    private readonly IGrainFactory _grainFactory;

    public CancelEventEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Post("/api/events/{eventId}/cancel");
        AllowAnonymous();
        Throttle(
            hitLimit: 10,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "Cancel a sport event";
            s.Description = "Cancels the specified sport event with a reason, changing its status to Cancelled";
            s.Params["eventId"] = "The unique identifier of the event";
            s.Response(200, "Event cancelled successfully");
            s.Response(400, "Invalid request");
            s.Response(404, "Event not found");
            s.Response(409, "Event cannot be cancelled in current status");
            s.Response(500, "Cancel operation failed");
        });
    }

    public override async Task HandleAsync(CancelEventRequest req, CancellationToken ct)
    {
        try
        {
            var eventGrain = _grainFactory.GetGrain<ISportEventGrain>(req.EventId);
            
            var sportEvent = await eventGrain.CancelEventAsync(req.Reason);

            Response = new EventResponse
            {
                IsSuccess = true,
                Event = MapEvent(sportEvent)
            };
        }
        catch (ArgumentException ex)
        {
            HttpContext.Response.StatusCode = 400;
            Response = new EventResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
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
            Logger.LogError(ex, "Failed to cancel event {EventId}", req.EventId);
            
            HttpContext.Response.StatusCode = 500;
            Response = new EventResponse
            {
                IsSuccess = false,
                ErrorMessage = "An error occurred while cancelling the event"
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