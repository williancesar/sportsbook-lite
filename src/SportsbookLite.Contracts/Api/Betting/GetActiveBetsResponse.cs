using SportsbookLite.Contracts.Betting;

namespace SportsbookLite.Contracts.Api.Betting;

[GenerateSerializer]
public sealed record GetActiveBetsResponse(
    [property: Id(0)] IReadOnlyList<Bet> ActiveBets,
    [property: Id(1)] int TotalCount,
    [property: Id(2)] bool Success,
    [property: Id(3)] string? Error
);