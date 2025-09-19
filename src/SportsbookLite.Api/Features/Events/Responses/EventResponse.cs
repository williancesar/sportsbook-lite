using SportsbookLite.Contracts.Events;

namespace SportsbookLite.Api.Features.Events.Responses;

public sealed record EventResponse
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public EventDto? Event { get; init; }
}

public sealed record EventDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string SportType { get; init; } = string.Empty;
    public string Competition { get; init; } = string.Empty;
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
    public string Status { get; init; } = string.Empty;
    public Dictionary<string, string> Participants { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastModified { get; init; }
}