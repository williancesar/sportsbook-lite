using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Odds.Responses;
using SportsbookLite.Contracts.Odds;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Odds.Endpoints;

public sealed class GetOddsEndpoint : EndpointWithoutRequest<OddsResponse>
{
    private readonly IGrainFactory _grainFactory;

    public GetOddsEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Get("/api/odds/{marketId}");
        AllowAnonymous();
        Throttle(
            hitLimit: 60,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "Get current odds for a market";
            s.Description = "Retrieves the current odds snapshot for all selections in a market";
            s.Params["marketId"] = "The unique identifier of the market";
            s.Response(200, "Odds retrieved successfully", example: new OddsResponse
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
                    },
                    ["team_b"] = new OddsDto
                    {
                        Decimal = 1.80m,
                        Fractional = 0.80m,
                        American = -125,
                        ImpliedProbability = 0.56m,
                        MarketId = "match_123_winner",
                        Selection = "team_b",
                        Source = OddsSource.Manual,
                        Timestamp = DateTimeOffset.UtcNow
                    }
                },
                SnapshotTime = DateTimeOffset.UtcNow,
                Volatility = OddsVolatility.Low,
                IsSuspended = false,
                TotalMargin = 4.0m
            });
            s.Response(404, "Market not found");
            s.Response(500, "Failed to retrieve odds");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
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
            var snapshot = await oddsGrain.GetCurrentOddsAsync();

            if (snapshot.Selections.Count == 0)
            {
                HttpContext.Response.StatusCode = 404;
                return;
            }

            Response = MapToOddsResponse(snapshot);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get odds for market {MarketId}", Route<string>("marketId"));
            
            HttpContext.Response.StatusCode = 500;
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