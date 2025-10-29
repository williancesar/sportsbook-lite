using System.ComponentModel.DataAnnotations;

namespace SportsbookLite.Api.Features.Events.Requests;

public sealed record CompleteEventRequest
{
    [Required]
    public Guid EventId { get; init; }

    [Required]
    public Dictionary<string, object> Results { get; init; } = new();

    public bool IsOfficial { get; init; } = true;
}