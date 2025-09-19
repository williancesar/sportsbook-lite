using System.ComponentModel.DataAnnotations;
using SportsbookLite.Contracts.Events;

namespace SportsbookLite.Api.Features.Events.Requests;

public sealed record ListEventsRequest
{
    public EventStatus? Status { get; init; }
    
    public SportType? SportType { get; init; }
    
    [StringLength(100, ErrorMessage = "Competition filter cannot exceed 100 characters")]
    public string? Competition { get; init; }
    
    [Range(1, 1000, ErrorMessage = "Page size must be between 1 and 1000")]
    public int PageSize { get; init; } = 50;
    
    [Range(1, int.MaxValue, ErrorMessage = "Page number must be greater than 0")]
    public int PageNumber { get; init; } = 1;
    
    public DateTimeOffset? StartTimeFrom { get; init; }
    
    public DateTimeOffset? StartTimeTo { get; init; }
}