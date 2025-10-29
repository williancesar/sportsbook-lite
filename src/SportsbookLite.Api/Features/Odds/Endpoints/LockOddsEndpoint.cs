using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Odds.Requests;
using SportsbookLite.Api.Features.Odds.Responses;
using SportsbookLite.Contracts.Odds;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Odds.Endpoints;

public sealed class LockOddsEndpoint : Endpoint<LockOddsRequest, LockResponse>
{
    private readonly IGrainFactory _grainFactory;

    public LockOddsEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Post("/api/odds/{marketId}/lock");
        AllowAnonymous();
        Throttle(
            hitLimit: 50,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "Lock odds for a specific bet";
            s.Description = "Locks odds for a selection to ensure consistent odds during bet placement";
            s.Params["marketId"] = "The unique identifier of the market";
            s.Response(200, "Odds locked successfully", example: new LockResponse
            {
                MarketId = "match_123_winner",
                BetId = "bet_456",
                SelectionId = "team_a",
                IsLocked = true,
                LockTimestamp = DateTimeOffset.UtcNow,
                LockedOdds = new OddsDto
                {
                    Decimal = 2.50m,
                    Fractional = 1.50m,
                    American = 150,
                    ImpliedProbability = 0.40m,
                    MarketId = "match_123_winner",
                    Selection = "team_a",
                    Source = OddsSource.Manual,
                    Timestamp = DateTimeOffset.UtcNow
                },
                Message = "Odds locked successfully"
            });
            s.Response(400, "Invalid request");
            s.Response(404, "Market or selection not found");
            s.Response(409, "Selection already locked or market suspended");
            s.Response(500, "Failed to lock odds");
        });
    }

    public override async Task HandleAsync(LockOddsRequest req, CancellationToken ct)
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
                    SelectionId = req.SelectionId,
                    Message = "Invalid market ID"
                };
                return;
            }

            var oddsGrain = _grainFactory.GetGrain<IOddsGrain>(marketId);
            
            var isSuspended = await oddsGrain.IsMarketSuspendedAsync();
            if (isSuspended)
            {
                HttpContext.Response.StatusCode = 409;
                Response = new LockResponse
                {
                    MarketId = marketId,
                    BetId = req.BetId,
                    SelectionId = req.SelectionId,
                    Message = "Market is suspended"
                };
                return;
            }

            var isAlreadyLocked = await oddsGrain.IsSelectionLockedAsync(req.SelectionId);
            if (isAlreadyLocked)
            {
                HttpContext.Response.StatusCode = 409;
                Response = new LockResponse
                {
                    MarketId = marketId,
                    BetId = req.BetId,
                    SelectionId = req.SelectionId,
                    Message = "Selection is already locked"
                };
                return;
            }

            var snapshot = await oddsGrain.LockOddsForBetAsync(req.BetId, req.SelectionId);

            if (!snapshot.Selections.ContainsKey(req.SelectionId))
            {
                HttpContext.Response.StatusCode = 404;
                Response = new LockResponse
                {
                    MarketId = marketId,
                    BetId = req.BetId,
                    SelectionId = req.SelectionId,
                    Message = "Selection not found"
                };
                return;
            }

            var lockedOdds = snapshot.Selections[req.SelectionId];

            Response = new LockResponse
            {
                MarketId = marketId,
                BetId = req.BetId,
                SelectionId = req.SelectionId,
                IsLocked = true,
                LockTimestamp = DateTimeOffset.UtcNow,
                LockedOdds = new OddsDto
                {
                    Decimal = lockedOdds.Decimal,
                    Fractional = lockedOdds.ToFractional(),
                    American = lockedOdds.ToAmerican(),
                    ImpliedProbability = lockedOdds.ImpliedProbability,
                    MarketId = lockedOdds.MarketId,
                    Selection = lockedOdds.Selection,
                    Source = lockedOdds.Source,
                    Timestamp = lockedOdds.Timestamp
                },
                Message = "Odds locked successfully"
            };
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning(ex, "Invalid lock request for market {MarketId}, bet {BetId}", Route<string>("marketId"), req.BetId);
            
            HttpContext.Response.StatusCode = 400;
            Response = new LockResponse
            {
                MarketId = Route<string>("marketId") ?? string.Empty,
                BetId = req.BetId,
                SelectionId = req.SelectionId,
                Message = "Invalid request parameters"
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to lock odds for market {MarketId}, bet {BetId}", Route<string>("marketId"), req.BetId);
            
            HttpContext.Response.StatusCode = 500;
            Response = new LockResponse
            {
                MarketId = Route<string>("marketId") ?? string.Empty,
                BetId = req.BetId,
                SelectionId = req.SelectionId,
                Message = "Failed to lock odds"
            };
        }
    }
}