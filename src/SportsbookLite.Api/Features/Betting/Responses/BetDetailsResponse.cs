namespace SportsbookLite.Api.Features.Betting.Responses;

public sealed record BetDetailsResponse
{
    public Guid BetId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public Guid EventId { get; init; }
    public string MarketId { get; init; } = string.Empty;
    public string SelectionId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal Odds { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public DateTimeOffset PlacedAt { get; init; }
    public DateTimeOffset? SettledAt { get; init; }
    public decimal? Payout { get; init; }
    public decimal PotentialPayout { get; init; }
    public string? RejectionReason { get; init; }
    public string? VoidReason { get; init; }
}