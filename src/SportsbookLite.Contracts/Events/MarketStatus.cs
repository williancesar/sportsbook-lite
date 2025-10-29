namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public enum MarketStatus
{
    [Id(0)]
    Open = 0,
    
    [Id(1)]
    Active = 1,
    
    [Id(2)]
    Suspended = 2,
    
    [Id(3)]
    Closed = 3,
    
    [Id(4)]
    Settled = 4
}