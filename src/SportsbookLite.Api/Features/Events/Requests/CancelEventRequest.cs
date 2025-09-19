using System.ComponentModel.DataAnnotations;

namespace SportsbookLite.Api.Features.Events.Requests;

public sealed record CancelEventRequest
{
    [Required]
    public Guid EventId { get; init; }

    [Required]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Reason must be between 1 and 500 characters")]
    public string Reason { get; init; } = string.Empty;
}