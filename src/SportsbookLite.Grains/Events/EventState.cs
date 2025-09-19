using SportsbookLite.Contracts.Events;

namespace SportsbookLite.Grains.Events;

[GenerateSerializer]
public sealed class EventState
{
    [Id(0)]
    public SportEvent? Event { get; set; }

    [Id(1)]
    public Dictionary<Guid, Market> Markets { get; set; } = new();

    [Id(2)]
    public EventResult? Result { get; set; }

    [Id(3)]
    public List<IDomainEvent> Events { get; set; } = new();

    [Id(4)]
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

    public bool IsInitialized => Event.HasValue;

    public void UpdateLastModified()
    {
        LastModified = DateTimeOffset.UtcNow;
    }

    public void AddEvent(IDomainEvent domainEvent)
    {
        Events.Add(domainEvent);
        UpdateLastModified();
    }

    public void AddMarket(Market market)
    {
        Markets[market.Id] = market;
        UpdateLastModified();
    }

    public void UpdateMarket(Market market)
    {
        Markets[market.Id] = market;
        UpdateLastModified();
    }

    public bool TryGetMarket(Guid marketId, out Market market)
    {
        return Markets.TryGetValue(marketId, out market);
    }

    public void SuspendAllMarkets(string reason)
    {
        var marketIds = Markets.Keys.ToList();
        foreach (var marketId in marketIds)
        {
            var market = Markets[marketId];
            if (market.Status == MarketStatus.Open)
            {
                Markets[marketId] = market.WithStatus(MarketStatus.Suspended);
            }
        }
        UpdateLastModified();
    }

    public IReadOnlyList<Market> GetActiveMarkets()
    {
        return Markets.Values
            .Where(m => m.Status is MarketStatus.Open or MarketStatus.Suspended)
            .ToList();
    }
}