using System.ComponentModel.DataAnnotations;
using SportsbookLite.Contracts.Events;

namespace SportsbookLite.Api.Features.Events.Requests;

public sealed record CreateEventRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public string Name { get; init; } = string.Empty;

    [Required]
    public SportType SportType { get; init; }

    [Required]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Competition must be between 1 and 100 characters")]
    public string Competition { get; init; } = string.Empty;

    [Required]
    public DateTimeOffset StartTime { get; init; }

    [Required]
    public Dictionary<string, string> Participants { get; init; } = new();
}