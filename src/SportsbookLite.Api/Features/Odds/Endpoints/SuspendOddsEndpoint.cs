using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Odds.Requests;
using SportsbookLite.Api.Features.Odds.Responses;
using SportsbookLite.Contracts.Odds;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Odds.Endpoints;

public sealed class SuspendOddsEndpoint : Endpoint<SuspendOddsRequest, OddsResponse>
{
    private readonly IGrainFactory _grainFactory;

    public SuspendOddsEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Post("/api/odds/{marketId}/suspend");
        AllowAnonymous();
        Throttle(
            hitLimit: 10,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "Suspend odds for a market";
            s.Description = "Suspends all odds in a market to prevent new bets";
            s.Params["marketId"] = "The unique identifier of the market";
            s.Response(200, "Market suspended successfully", example: new OddsResponse
            {
                MarketId = "match_123_winner",
                Selections = new Dictionary<string, OddsDto>
                {
                    ["team_a"] = new OddsDto
                    {
                        Decimal = 2.50m,
                        Fractional = 1.50m,
                        American = 150,
                        ImpliedProbability = 0.40m,
                        MarketId = "match_123_winner",
                        Selection = "team_a",
                        Source = OddsSource.Manual,
                        Timestamp = DateTimeOffset.UtcNow
                    }
                },
                SnapshotTime = DateTimeOffset.UtcNow,
                Volatility = OddsVolatility.Low,
                IsSuspended = true,
                SuspensionReason = "High volatility detected",
                TotalMargin = 5.0m
            });
            s.Response(400, "Invalid request");
            s.Response(404, "Market not found");
            s.Response(409, "Market already suspended");
            s.Response(500, "Failed to suspend market");
        });
    }

    public override async Task HandleAsync(SuspendOddsRequest req, CancellationToken ct)
    {
        try
        {
            var marketId = Route<string>("marketId");
            if (string.IsNullOrEmpty(marketId))
            {
                HttpContext.Response.StatusCode = 400;
                return;
            }

            var oddsGrain = _grainFactory.GetGrain<IOddsGrain>(marketId);
            
            var isAlreadySuspended = await oddsGrain.IsMarketSuspendedAsync();
            if (isAlreadySuspended)
            {
                HttpContext.Response.StatusCode = 409;
                Response = new OddsResponse { MarketId = marketId };
                return;
            }

            var snapshot = await oddsGrain.SuspendOddsAsync(req.Reason, req.SuspendedBy);

            if (snapshot.Selections.Count == 0)
            {
                HttpContext.Response.StatusCode = 404;
                return;
            }

            Response = MapToOddsResponse(snapshot);
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning(ex, "Invalid suspend request for market {MarketId}", Route<string>("marketId"));
            
            HttpContext.Response.StatusCode = 400;
            Response = new OddsResponse
            {
                MarketId = Route<string>("marketId") ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to suspend market {MarketId}", Route<string>("marketId"));
            
            HttpContext.Response.StatusCode = 500;
            Response = new OddsResponse
            {
                MarketId = Route<string>("marketId") ?? string.Empty
            };
        }
    }

    private static OddsResponse MapToOddsResponse(OddsSnapshot snapshot)
    {
        var selections = new Dictionary<string, OddsDto>();
        
        foreach (var (selection, odds) in snapshot.Selections)
        {
            selections[selection] = new OddsDto
            {
                Decimal = odds.Decimal,
                Fractional = odds.ToFractional(),
                American = odds.ToAmerican(),
                ImpliedProbability = odds.ImpliedProbability,
                MarketId = odds.MarketId,
                Selection = odds.Selection,
                Source = odds.Source,
                Timestamp = odds.Timestamp
            };
        }

        return new OddsResponse
        {
            MarketId = snapshot.MarketId,
            Selections = selections,
            SnapshotTime = snapshot.SnapshotTime,
            Volatility = snapshot.Volatility,
            IsSuspended = snapshot.IsSuspended,
            SuspensionReason = snapshot.SuspensionReason,
            TotalMargin = snapshot.GetTotalMargin()
        };
    }
}