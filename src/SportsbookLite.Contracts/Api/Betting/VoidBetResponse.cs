using SportsbookLite.Contracts.Betting;

namespace SportsbookLite.Contracts.Api.Betting;

[GenerateSerializer]
public sealed record VoidBetResponse(
    [property: Id(0)] Bet? Bet,
    [property: Id(1)] bool Success,
    [property: Id(2)] string? Error
);