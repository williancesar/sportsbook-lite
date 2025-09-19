using SportsbookLite.Contracts.Betting;

namespace SportsbookLite.Contracts.Api.Betting;

[GenerateSerializer]
public sealed record GetUserBetsResponse(
    [property: Id(0)] IReadOnlyList<Bet> Bets,
    [property: Id(1)] int TotalCount,
    [property: Id(2)] bool Success,
    [property: Id(3)] string? Error
);