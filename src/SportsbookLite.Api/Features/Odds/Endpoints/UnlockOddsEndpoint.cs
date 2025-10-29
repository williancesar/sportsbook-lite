using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Odds.Requests;
using SportsbookLite.Api.Features.Odds.Responses;
using SportsbookLite.Contracts.Odds;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Odds.Endpoints;

public sealed class UnlockOddsEndpoint : Endpoint<UnlockOddsRequest, LockResponse>
{
    private readonly IGrainFactory _grainFactory;

    public UnlockOddsEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Post("/api/odds/{marketId}/unlock");
        AllowAnonymous();
        Throttle(
            hitLimit: 50,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "Unlock odds for a specific bet";
            s.Description = "Unlocks previously locked odds, typically after bet completion or cancellation";
            s.Params["marketId"] = "The unique identifier of the market";
            s.Response(200, "Odds unlocked successfully", example: new LockResponse
            {
                MarketId = "match_123_winner",
                BetId = "bet_456",
                SelectionId = "team_a",
                IsLocked = false,
                LockTimestamp = DateTimeOffset.UtcNow,
                Message = "Odds unlocked successfully"
            });
            s.Response(400, "Invalid request");
            s.Response(404, "Market or bet not found");
            s.Response(409, "Bet is not currently locked");
            s.Response(500, "Failed to unlock odds");
        });
    }

    public override async Task HandleAsync(UnlockOddsRequest req, CancellationToken ct)
    {
        try
        {
            var marketId = Route<string>("marketId");
            if (string.IsNullOrEmpty(marketId))
            {
                HttpContext.Response.StatusCode = 400;
                Response = new LockResponse
                {
                    MarketId = marketId ?? string.Empty,
                    BetId = req.BetId,
                    Message = "Invalid market ID"
                };
                return;
            }

            var oddsGrain = _grainFactory.GetGrain<IOddsGrain>(marketId);
            
            var lockedSelections = await oddsGrain.GetLockedSelectionsAsync();
            var betLocks = lockedSelections.FirstOrDefault(kvp => kvp.Value.Contains(req.BetId));

            if (betLocks.Key == null)
            {
                HttpContext.Response.StatusCode = 404;
                Response = new LockResponse
                {
                    MarketId = marketId,
                    BetId = req.BetId,
                    Message = "Bet lock not found"
                };
                return;
            }

            var snapshot = await oddsGrain.UnlockOddsAsync(req.BetId);

            Response = new LockResponse
            {
                MarketId = marketId,
                BetId = req.BetId,
                SelectionId = betLocks.Key,
                IsLocked = false,
                LockTimestamp = DateTimeOffset.UtcNow,
                Message = "Odds unlocked successfully"
            };
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning(ex, "Invalid unlock request for market {MarketId}, bet {BetId}", Route<string>("marketId"), req.BetId);
            
            HttpContext.Response.StatusCode = 400;
            Response = new LockResponse
            {
                MarketId = Route<string>("marketId") ?? string.Empty,
                BetId = req.BetId,
                Message = "Invalid request parameters"
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to unlock odds for market {MarketId}, bet {BetId}", Route<string>("marketId"), req.BetId);
            
            HttpContext.Response.StatusCode = 500;
            Response = new LockResponse
            {
                MarketId = Route<string>("marketId") ?? string.Empty,
                BetId = req.BetId,
                Message = "Failed to unlock odds"
            };
        }
    }
}