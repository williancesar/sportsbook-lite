namespace SportsbookLite.Grains.Betting;

[GenerateSerializer]
public sealed class BetState
{
    [Id(0)]
    public Guid BetId { get; set; } = Guid.Empty;

    [Id(1)]
    public string UserId { get; set; } = string.Empty;

    [Id(2)]
    public Guid EventId { get; set; } = Guid.Empty;

    [Id(3)]
    public string MarketId { get; set; } = string.Empty;

    [Id(4)]
    public string SelectionId { get; set; } = string.Empty;

    [Id(5)]
    public decimal Amount { get; set; } = 0m;

    [Id(6)]
    public string Currency { get; set; } = "USD";

    [Id(7)]
    public decimal Odds { get; set; } = 0m;

    [Id(8)]
    public BetStatus Status { get; set; } = BetStatus.Pending;

    [Id(9)]
    public BetType Type { get; set; } = BetType.Single;

    [Id(10)]
    public DateTimeOffset PlacedAt { get; set; } = DateTimeOffset.UtcNow;

    [Id(11)]
    public DateTimeOffset? SettledAt { get; set; }

    [Id(12)]
    public decimal? PayoutAmount { get; set; }

    [Id(13)]
    public string? RejectionReason { get; set; }

    [Id(14)]
    public string? VoidReason { get; set; }

    [Id(15)]
    public long Version { get; set; } = 0;

    [Id(16)]
    public List<IDomainEvent> UncommittedEvents { get; set; } = new();

    public Bet ToBet()
    {
        return new Bet(
            BetId,
            UserId,
            EventId,
            MarketId,
            SelectionId,
            new SportsbookLite.Contracts.Wallet.Money(Amount, Currency),
            Odds,
            Status,
            Type,
            PlacedAt,
            SettledAt,
            PayoutAmount.HasValue ? new SportsbookLite.Contracts.Wallet.Money(PayoutAmount.Value, Currency) : null,
            RejectionReason,
            VoidReason
        );
    }

    public void AddUncommittedEvent(IDomainEvent domainEvent)
    {
        UncommittedEvents.Add(domainEvent);
        Version++;
    }

    public void MarkEventsAsCommitted()
    {
        UncommittedEvents.Clear();
    }
}