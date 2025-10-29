using System.ComponentModel.DataAnnotations;

namespace SportsbookLite.Api.Features.Events.Requests;

public sealed record UpdateEventRequest
{
    [Required]
    public Guid EventId { get; init; }

    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public string? Name { get; init; }

    public DateTimeOffset? StartTime { get; init; }

    public Dictionary<string, string>? Participants { get; init; }
}