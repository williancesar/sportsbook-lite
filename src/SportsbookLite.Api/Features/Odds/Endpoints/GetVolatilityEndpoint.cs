using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Odds.Requests;
using SportsbookLite.Api.Features.Odds.Responses;
using SportsbookLite.Contracts.Odds;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Odds.Endpoints;

public sealed class GetVolatilityEndpoint : Endpoint<GetVolatilityRequest, VolatilityResponse>
{
    private readonly IGrainFactory _grainFactory;

    public GetVolatilityEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Get("/api/odds/{marketId}/volatility");
        AllowAnonymous();
        Throttle(
            hitLimit: 30,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "Get market volatility metrics";
            s.Description = "Retrieves volatility analysis for a market within a specified time window";
            s.Params["marketId"] = "The unique identifier of the market";
            s.Response(200, "Volatility metrics retrieved successfully", example: new VolatilityResponse
            {
                MarketId = "match_123_winner",
                Level = OddsVolatility.Medium,
                Score = 15.75m,
                SelectionMetrics = new Dictionary<string, VolatilityMetrics>
                {
                    ["team_a"] = new VolatilityMetrics
                    {
                        Selection = "team_a",
                        VolatilityScore = 12.5m,
                        Level = OddsVolatility.Medium,
                        UpdateCount = 8,
                        MaxPercentageChange = 8.33m,
                        AveragePercentageChange = 3.25m
                    }
                },
                CalculatedAt = DateTimeOffset.UtcNow,
                Window = TimeSpan.FromHours(1)
            });
            s.Response(404, "Market not found");
            s.Response(500, "Failed to calculate volatility");
        });
    }

    public override async Task HandleAsync(GetVolatilityRequest req, CancellationToken ct)
    {
        try
        {
            var marketId = Route<string>("marketId");
            if (string.IsNullOrEmpty(marketId))
            {
                HttpContext.Response.StatusCode = 400;
                return;
            }

            var windowHours = Math.Max(1, Math.Min(req.WindowHours, 24));
            var window = TimeSpan.FromHours(windowHours);

            var oddsGrain = _grainFactory.GetGrain<IOddsGrain>(marketId);
            
            var volatility = await oddsGrain.CalculateVolatilityAsync(window);
            var volatilityScore = await oddsGrain.GetVolatilityScoreAsync(window);
            var allHistory = await oddsGrain.GetAllOddsHistoryAsync();

            if (allHistory.Count == 0)
            {
                HttpContext.Response.StatusCode = 404;
                return;
            }

            var selectionMetrics = new Dictionary<string, VolatilityMetrics>();

            foreach (var (selection, history) in allHistory)
            {
                var recentUpdates = history.GetUpdatesInTimeWindow(window).ToList();
                var selectionVolatilityScore = history.CalculateVolatilityScore(window);
                var selectionVolatilityLevel = history.GetVolatilityLevel(window);

                selectionMetrics[selection] = new VolatilityMetrics
                {
                    Selection = selection,
                    VolatilityScore = selectionVolatilityScore,
                    Level = selectionVolatilityLevel,
                    UpdateCount = recentUpdates.Count,
                    MaxPercentageChange = recentUpdates.Any() 
                        ? recentUpdates.Max(u => u.PercentageChange) 
                        : 0,
                    AveragePercentageChange = recentUpdates.Any() 
                        ? recentUpdates.Average(u => u.PercentageChange) 
                        : 0
                };
            }

            Response = new VolatilityResponse
            {
                MarketId = marketId,
                Level = volatility,
                Score = volatilityScore,
                SelectionMetrics = selectionMetrics,
                CalculatedAt = DateTimeOffset.UtcNow,
                Window = window
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get volatility for market {MarketId}", Route<string>("marketId"));
            
            HttpContext.Response.StatusCode = 500;
        }
    }
}