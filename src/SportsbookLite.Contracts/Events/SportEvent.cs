namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public readonly record struct SportEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] string Name,
    [property: Id(2)] SportType SportType,
    [property: Id(3)] string Competition,
    [property: Id(4)] DateTimeOffset StartTime,
    [property: Id(5)] DateTimeOffset? EndTime,
    [property: Id(6)] EventStatus Status,
    [property: Id(7)] Dictionary<string, string> Participants,
    [property: Id(8)] DateTimeOffset CreatedAt,
    [property: Id(9)] DateTimeOffset LastModified)
{
    public static SportEvent Create(
        string name,
        SportType sportType,
        string competition,
        DateTimeOffset startTime,
        Dictionary<string, string> participants)
    {
        var now = DateTimeOffset.UtcNow;
        return new SportEvent(
            Id: Guid.NewGuid(),
            Name: name,
            SportType: sportType,
            Competition: competition,
            StartTime: startTime,
            EndTime: null,
            Status: EventStatus.Scheduled,
            Participants: participants,
            CreatedAt: now,
            LastModified: now);
    }

    public SportEvent WithStatus(EventStatus status, DateTimeOffset? endTime = null)
    {
        return this with 
        { 
            Status = status, 
            EndTime = endTime ?? EndTime,
            LastModified = DateTimeOffset.UtcNow 
        };
    }

    public SportEvent WithUpdatedTime(DateTimeOffset newStartTime)
    {
        return this with 
        { 
            StartTime = newStartTime,
            LastModified = DateTimeOffset.UtcNow 
        };
    }

    public bool CanTransitionTo(EventStatus newStatus)
    {
        return newStatus switch
        {
            EventStatus.Scheduled => Status == EventStatus.Suspended,
            EventStatus.Live => Status is EventStatus.Scheduled or EventStatus.Suspended,
            EventStatus.Completed => Status == EventStatus.Live,
            EventStatus.Cancelled => Status is EventStatus.Scheduled or EventStatus.Suspended,
            EventStatus.Suspended => Status is EventStatus.Scheduled or EventStatus.Live,
            _ => false
        };
    }
}