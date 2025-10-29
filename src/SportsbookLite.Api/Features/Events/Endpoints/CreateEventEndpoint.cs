using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Events.Requests;
using SportsbookLite.Api.Features.Events.Responses;
using SportsbookLite.Contracts.Events;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Events.Endpoints;

public sealed class CreateEventEndpoint : Endpoint<CreateEventRequest, EventResponse>
{
    private readonly IGrainFactory _grainFactory;

    public CreateEventEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Post("/api/events");
        AllowAnonymous();
        Throttle(
            hitLimit: 10,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "Create a new sport event";
            s.Description = "Creates a new sport event with the specified details";
            s.Response(201, "Event created successfully", example: new EventResponse
            {
                IsSuccess = true,
                Event = new EventDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Championship Final",
                    SportType = "Football",
                    Competition = "Premier League",
                    StartTime = DateTimeOffset.UtcNow.AddDays(1),
                    Status = "Scheduled",
                    Participants = new Dictionary<string, string> { ["home"] = "Team A", ["away"] = "Team B" },
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastModified = DateTimeOffset.UtcNow
                }
            });
            s.Response(400, "Invalid request");
            s.Response(500, "Event creation failed");
        });
    }

    public override async Task HandleAsync(CreateEventRequest req, CancellationToken ct)
    {
        try
        {
            var eventId = Guid.NewGuid();
            var eventGrain = _grainFactory.GetGrain<ISportEventGrain>(eventId);
            
            var sportEvent = await eventGrain.CreateEventAsync(
                req.Name,
                req.SportType,
                req.Competition,
                req.StartTime,
                req.Participants);

            HttpContext.Response.StatusCode = 201;
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
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create event");
            
            HttpContext.Response.StatusCode = 500;
            Response = new EventResponse
            {
                IsSuccess = false,
                ErrorMessage = "An error occurred while creating the event"
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