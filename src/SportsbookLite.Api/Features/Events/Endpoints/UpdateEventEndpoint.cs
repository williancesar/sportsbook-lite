using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Events.Requests;
using SportsbookLite.Api.Features.Events.Responses;
using SportsbookLite.Contracts.Events;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Events.Endpoints;

public sealed class UpdateEventEndpoint : Endpoint<UpdateEventRequest, EventResponse>
{
    private readonly IGrainFactory _grainFactory;

    public UpdateEventEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Put("/api/events/{eventId}");
        AllowAnonymous();
        Throttle(
            hitLimit: 20,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "Update an existing sport event";
            s.Description = "Updates the specified sport event with new details";
            s.Params["eventId"] = "The unique identifier of the event";
            s.Response(200, "Event updated successfully");
            s.Response(400, "Invalid request");
            s.Response(404, "Event not found");
            s.Response(409, "Event cannot be updated in current status");
            s.Response(500, "Update failed");
        });
    }

    public override async Task HandleAsync(UpdateEventRequest req, CancellationToken ct)
    {
        try
        {
            var eventGrain = _grainFactory.GetGrain<ISportEventGrain>(req.EventId);
            
            var sportEvent = await eventGrain.UpdateEventAsync(
                req.Name,
                req.StartTime,
                req.Participants);

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
            Logger.LogError(ex, "Failed to update event {EventId}", req.EventId);
            
            HttpContext.Response.StatusCode = 500;
            Response = new EventResponse
            {
                IsSuccess = false,
                ErrorMessage = "An error occurred while updating the event"
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