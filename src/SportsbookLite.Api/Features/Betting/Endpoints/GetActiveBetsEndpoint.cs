using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Betting.Responses;
using SportsbookLite.Contracts.Betting;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Betting.Endpoints;

public sealed class GetActiveBetsEndpoint : EndpointWithoutRequest<UserBetsResponse>
{
    private readonly IGrainFactory _grainFactory;

    public GetActiveBetsEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Get("/api/users/{userId}/bets/active");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get active bets";
            s.Description = "Retrieves all active (non-settled) bets for a specific user";
            s.Params["userId"] = "The unique identifier of the user";
            s.Response(200, "Active bets retrieved successfully", example: new UserBetsResponse
            {
                Bets = new[]
                {
                    new BetSummaryDto
                    {
                        BetId = Guid.NewGuid(),
                        EventId = Guid.NewGuid(),
                        MarketId = "match_winner",
                        SelectionId = "team_a",
                        Amount = 100.00m,
                        Currency = "USD",
                        Odds = 1.5m,
                        Status = "Accepted",
                        PotentialPayout = 150.00m,
                        PlacedAt = DateTimeOffset.UtcNow
                    }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 50,
                HasNextPage = false
            });
            s.Response(500, "Internal server error");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var userId = Route<string>("userId")!;
            var betManagerGrain = _grainFactory.GetGrain<IBetManagerGrain>(userId);
            var activeBets = await betManagerGrain.GetActiveBetsAsync();

            Response = new UserBetsResponse
            {
                Bets = activeBets.Select(MapToBetSummaryDto).ToArray(),
                TotalCount = activeBets.Count,
                Page = 1,
                PageSize = activeBets.Count,
                HasNextPage = false
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get active bets for user {UserId}", Route<string>("userId"));
            
            HttpContext.Response.StatusCode = 500;
            Response = new UserBetsResponse();
        }
    }

    private static BetSummaryDto MapToBetSummaryDto(Bet bet) =>
        new()
        {
            BetId = bet.Id,
            EventId = bet.EventId,
            MarketId = bet.MarketId,
            SelectionId = bet.SelectionId,
            Amount = bet.Amount.Amount,
            Currency = bet.Amount.Currency,
            Odds = bet.Odds,
            Status = bet.Status.ToString(),
            PotentialPayout = bet.PotentialPayout.Amount,
            PlacedAt = bet.PlacedAt,
            SettledAt = bet.SettledAt,
            ActualPayout = bet.Payout?.Amount
        };
}