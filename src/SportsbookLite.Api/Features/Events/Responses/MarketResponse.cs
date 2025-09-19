using SportsbookLite.Contracts.Events;

namespace SportsbookLite.Api.Features.Events.Responses;

public sealed record MarketResponse
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public MarketDto? Market { get; init; }
}

public sealed record MarketDto
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Dictionary<string, decimal> Outcomes { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastModified { get; init; }
    public string? WinningOutcome { get; init; }
}