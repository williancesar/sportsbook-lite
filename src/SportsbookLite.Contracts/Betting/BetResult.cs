namespace SportsbookLite.Contracts.Betting;

[GenerateSerializer]
public sealed record BetResult(
    [property: Id(0)] bool IsSuccess,
    [property: Id(1)] Bet? Bet,
    [property: Id(2)] string? Error,
    [property: Id(3)] DateTimeOffset Timestamp
)
{
    public static BetResult Success(Bet bet) => new(true, bet, null, DateTimeOffset.UtcNow);
    
    public static BetResult Failed(string error) => new(false, null, error, DateTimeOffset.UtcNow);
}