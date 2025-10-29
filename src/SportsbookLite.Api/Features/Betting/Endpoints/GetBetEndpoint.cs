using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Betting.Responses;
using SportsbookLite.Contracts.Betting;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Betting.Endpoints;

public sealed class GetBetEndpoint : EndpointWithoutRequest<BetDetailsResponse>
{
    private readonly IGrainFactory _grainFactory;

    public GetBetEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Get("/api/bets/{betId}");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get bet details";
            s.Description = "Retrieves detailed information about a specific bet";
            s.Params["betId"] = "The unique identifier of the bet";
            s.Response(200, "Bet found", example: new BetDetailsResponse
            {
                BetId = Guid.NewGuid(),
                UserId = "user_123",
                EventId = Guid.NewGuid(),
                MarketId = "match_winner",
                SelectionId = "team_a",
                Amount = 100.00m,
                Currency = "USD",
                Odds = 1.5m,
                Status = "Accepted",
                Type = "Single",
                PlacedAt = DateTimeOffset.UtcNow,
                PotentialPayout = 150.00m
            });
            s.Response(404, "Bet not found");
            s.Response(500, "Internal server error");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var betIdStr = Route<string>("betId")!;
            if (!Guid.TryParse(betIdStr, out var betId))
            {
                HttpContext.Response.StatusCode = 400;
                return;
            }

            var betGrain = _grainFactory.GetGrain<IBetGrain>(betId);
            var bet = await betGrain.GetBetDetailsAsync();

            if (bet == null)
            {
                HttpContext.Response.StatusCode = 404;
                return;
            }

            Response = MapToBetDetailsResponse(bet);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get bet details for bet {BetId}", Route<string>("betId"));
            
            HttpContext.Response.StatusCode = 500;
        }
    }

    private static BetDetailsResponse MapToBetDetailsResponse(Bet bet) =>
        new()
        {
            BetId = bet.Id,
            UserId = bet.UserId,
            EventId = bet.EventId,
            MarketId = bet.MarketId,
            SelectionId = bet.SelectionId,
            Amount = bet.Amount.Amount,
            Currency = bet.Amount.Currency,
            Odds = bet.Odds,
            Status = bet.Status.ToString(),
            Type = bet.Type.ToString(),
            PlacedAt = bet.PlacedAt,
            SettledAt = bet.SettledAt,
            Payout = bet.Payout?.Amount,
            PotentialPayout = bet.PotentialPayout.Amount,
            RejectionReason = bet.RejectionReason,
            VoidReason = bet.VoidReason
        };
}