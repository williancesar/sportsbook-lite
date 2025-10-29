namespace SportsbookLite.Infrastructure.EventStore;

[GenerateSerializer]
public sealed record EventMetadata(
    [property: Id(0)] string EventType,
    [property: Id(1)] string AggregateId,
    [property: Id(2)] long Version,
    [property: Id(3)] DateTimeOffset Timestamp,
    [property: Id(4)] string? CorrelationId = null,
    [property: Id(5)] string? CausationId = null,
    [property: Id(6)] string? UserId = null,
    [property: Id(7)] Dictionary<string, string>? Properties = null
);