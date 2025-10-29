namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public readonly record struct Market(
    [property: Id(0)] Guid Id,
    [property: Id(1)] Guid EventId,
    [property: Id(2)] string Name,
    [property: Id(3)] string Description,
    [property: Id(4)] MarketStatus Status,
    [property: Id(5)] Dictionary<string, decimal> Outcomes,
    [property: Id(6)] DateTimeOffset CreatedAt,
    [property: Id(7)] DateTimeOffset LastModified,
    [property: Id(8)] string? WinningOutcome = null)
{
    public static Market Create(
        Guid eventId,
        string name,
        string description,
        Dictionary<string, decimal> outcomes)
    {
        var now = DateTimeOffset.UtcNow;
        return new Market(
            Id: Guid.NewGuid(),
            EventId: eventId,
            Name: name,
            Description: description,
            Status: MarketStatus.Open,
            Outcomes: outcomes,
            CreatedAt: now,
            LastModified: now);
    }

    public Market WithStatus(MarketStatus status)
    {
        return this with 
        { 
            Status = status,
            LastModified = DateTimeOffset.UtcNow 
        };
    }

    public Market WithOutcomes(Dictionary<string, decimal> outcomes)
    {
        return this with 
        { 
            Outcomes = outcomes,
            LastModified = DateTimeOffset.UtcNow 
        };
    }

    public Market WithWinner(string winningOutcome)
    {
        return this with 
        { 
            WinningOutcome = winningOutcome,
            Status = MarketStatus.Settled,
            LastModified = DateTimeOffset.UtcNow 
        };
    }

    public bool CanTransitionTo(MarketStatus newStatus)
    {
        return newStatus switch
        {
            MarketStatus.Open => Status == MarketStatus.Suspended,
            MarketStatus.Suspended => Status is MarketStatus.Open,
            MarketStatus.Closed => Status is MarketStatus.Open or MarketStatus.Suspended,
            MarketStatus.Settled => Status == MarketStatus.Closed,
            _ => false
        };
    }
}