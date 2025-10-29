namespace SportsbookLite.Api.Features.Betting.Responses;

public sealed record BetHistoryResponse
{
    public IReadOnlyList<BetHistoryEventDto> Events { get; init; } = Array.Empty<BetHistoryEventDto>();
}

public sealed record BetHistoryEventDto
{
    public string EventType { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public Dictionary<string, object> Data { get; init; } = new();
    public Dictionary<string, object>? Metadata { get; init; }
}