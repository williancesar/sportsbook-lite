namespace SportsbookLite.Api.Features.Betting.Responses;

public sealed record UserBetsResponse
{
    public IReadOnlyList<BetSummaryDto> Bets { get; init; } = Array.Empty<BetSummaryDto>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasNextPage { get; init; }
}

public sealed record BetSummaryDto
{
    public Guid BetId { get; init; }
    public Guid EventId { get; init; }
    public string MarketId { get; init; } = string.Empty;
    public string SelectionId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal Odds { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal PotentialPayout { get; init; }
    public DateTimeOffset PlacedAt { get; init; }
    public DateTimeOffset? SettledAt { get; init; }
    public decimal? ActualPayout { get; init; }
}