namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public enum EventStatus
{
    [Id(0)]
    Scheduled = 0,
    
    [Id(1)]
    Live = 1,
    
    [Id(2)]
    Completed = 2,
    
    [Id(3)]
    Cancelled = 3,
    
    [Id(4)]
    Suspended = 4
}