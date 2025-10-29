using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Events.Requests;
using SportsbookLite.Api.Features.Events.Responses;
using SportsbookLite.Contracts.Events;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Events.Endpoints;

public sealed class CompleteEventEndpoint : Endpoint<CompleteEventRequest, EventResponse>
{
    private readonly IGrainFactory _grainFactory;

    public CompleteEventEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Post("/api/events/{eventId}/complete");
        AllowAnonymous();
        Throttle(
            hitLimit: 10,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "Complete a sport event";
            s.Description = "Completes the specified sport event with results, changing its status to Completed";
            s.Params["eventId"] = "The unique identifier of the event";
            s.Response(200, "Event completed successfully");
            s.Response(400, "Invalid request");
            s.Response(404, "Event not found");
            s.Response(409, "Event cannot be completed in current status");
            s.Response(500, "Complete operation failed");
        });
    }

    public override async Task HandleAsync(CompleteEventRequest req, CancellationToken ct)
    {
        try
        {
            var eventGrain = _grainFactory.GetGrain<ISportEventGrain>(req.EventId);
            
            var eventResult = EventResult.Create(req.EventId, req.Results, req.IsOfficial);
            var sportEvent = await eventGrain.CompleteEventAsync(eventResult);

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
            Logger.LogError(ex, "Failed to complete event {EventId}", req.EventId);
            
            HttpContext.Response.StatusCode = 500;
            Response = new EventResponse
            {
                IsSuccess = false,
                ErrorMessage = "An error occurred while completing the event"
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