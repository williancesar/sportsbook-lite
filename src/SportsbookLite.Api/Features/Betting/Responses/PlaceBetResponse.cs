namespace SportsbookLite.Api.Features.Betting.Responses;

public sealed record PlaceBetResponse
{
    public bool IsSuccess { get; init; }
    public Guid? BetId { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal? PotentialPayout { get; init; }
    public string? Currency { get; init; }
    public decimal? ActualOdds { get; init; }
    public DateTimeOffset? PlacedAt { get; init; }
    public string? ErrorMessage { get; init; }
}