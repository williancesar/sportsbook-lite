namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public readonly record struct EventResult(
    [property: Id(0)] Guid EventId,
    [property: Id(1)] Dictionary<string, object> Results,
    [property: Id(2)] DateTimeOffset ResultTime,
    [property: Id(3)] bool IsOfficial = true)
{
    public static EventResult Create(Guid eventId, Dictionary<string, object> results, bool isOfficial = true)
    {
        return new EventResult(
            EventId: eventId,
            Results: results,
            ResultTime: DateTimeOffset.UtcNow,
            IsOfficial: isOfficial);
    }

    public EventResult MakeOfficial()
    {
        return this with { IsOfficial = true };
    }

    public T? GetResult<T>(string key) where T : class
    {
        return Results.TryGetValue(key, out var value) ? value as T : null;
    }

    public bool TryGetResult<T>(string key, out T? result) where T : class
    {
        result = null;
        if (Results.TryGetValue(key, out var value) && value is T typedValue)
        {
            result = typedValue;
            return true;
        }
        return false;
    }
}