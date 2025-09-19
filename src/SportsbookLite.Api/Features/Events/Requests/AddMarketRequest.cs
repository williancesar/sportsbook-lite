using System.ComponentModel.DataAnnotations;

namespace SportsbookLite.Api.Features.Events.Requests;

public sealed record AddMarketRequest
{
    [Required]
    public Guid EventId { get; init; }

    [Required]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
    public string Name { get; init; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Description must be between 1 and 500 characters")]
    public string Description { get; init; } = string.Empty;

    [Required]
    public Dictionary<string, decimal> Outcomes { get; init; } = new();
}