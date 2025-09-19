using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Odds.Requests;
using SportsbookLite.Api.Features.Odds.Responses;
using SportsbookLite.Contracts.Odds;
using OddsValue = SportsbookLite.Contracts.Odds.Odds;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Odds.Endpoints;

public sealed class GetOddsHistoryEndpoint : Endpoint<GetOddsHistoryRequest, OddsHistoryResponse>
{
    private readonly IGrainFactory _grainFactory;

    public GetOddsHistoryEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Get("/api/odds/{marketId}/history");
        AllowAnonymous();
        Throttle(
            hitLimit: 30,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "Get odds history for a market";
            s.Description = "Retrieves historical odds data for a specific selection or all selections in a market";
            s.Params["marketId"] = "The unique identifier of the market";
            s.Response(200, "Odds history retrieved successfully", example: new OddsHistoryResponse
            {
                MarketId = "match_123_winner",
                Selection = "team_a",
                Updates = new List<OddsUpdateDto>
                {
                    new OddsUpdateDto
                    {
                        PreviousOdds = new OddsDto
                        {
                            Decimal = 2.40m,
                            Fractional = 1.40m,
                            American = 140,
                            ImpliedProbability = 0.42m
                        },
                        NewOdds = new OddsDto
                        {
                            Decimal = 2.50m,
                            Fractional = 1.50m,
                            American = 150,
                            ImpliedProbability = 0.40m
                        },
                        PercentageChange = 4.17m,
                        Source = OddsSource.Manual,
                        UpdatedAt = DateTimeOffset.UtcNow
                    }
                },
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                LastModified = DateTimeOffset.UtcNow,
                TotalUpdates = 5
            });
            s.Response(404, "Market or selection not found");
            s.Response(500, "Failed to retrieve odds history");
        });
    }

    public override async Task HandleAsync(GetOddsHistoryRequest req, CancellationToken ct)
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

            if (!string.IsNullOrEmpty(req.Selection))
            {
                var history = await oddsGrain.GetOddsHistoryAsync(req.Selection);
                
                if (history.Updates.Count == 0)
                {
                    HttpContext.Response.StatusCode = 404;
                    return;
                }

                Response = MapToOddsHistoryResponse(history, req.StartDate, req.EndDate, req.Limit);
            }
            else
            {
                var allHistory = await oddsGrain.GetAllOddsHistoryAsync();
                
                if (allHistory.Count == 0)
                {
                    HttpContext.Response.StatusCode = 404;
                    return;
                }

                var firstSelection = allHistory.First();
                Response = MapToOddsHistoryResponse(firstSelection.Value, req.StartDate, req.EndDate, req.Limit);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get odds history for market {MarketId}", Route<string>("marketId"));
            
            HttpContext.Response.StatusCode = 500;
        }
    }

    private static OddsHistoryResponse MapToOddsHistoryResponse(
        OddsHistory history, 
        DateTimeOffset? startDate, 
        DateTimeOffset? endDate, 
        int limit)
    {
        var updates = history.Updates.AsEnumerable();

        if (startDate.HasValue)
            updates = updates.Where(u => u.UpdatedAt >= startDate.Value);

        if (endDate.HasValue)
            updates = updates.Where(u => u.UpdatedAt <= endDate.Value);

        var limitedUpdates = updates.Take(limit).Select(update => new OddsUpdateDto
        {
            PreviousOdds = MapToOddsDto(update.PreviousOdds),
            NewOdds = MapToOddsDto(update.NewOdds),
            PercentageChange = update.PercentageChange,
            Source = update.UpdateSource,
            UpdateReason = update.Reason,
            UpdatedAt = update.UpdatedAt
        }).ToList();

        var currentOdds = history.GetCurrentOdds();

        return new OddsHistoryResponse
        {
            MarketId = history.MarketId,
            Selection = history.Selection,
            Updates = limitedUpdates,
            CreatedAt = history.CreatedAt,
            LastModified = history.LastModified,
            TotalUpdates = history.Updates.Count,
            CurrentOdds = currentOdds.HasValue ? MapToOddsDto(currentOdds.Value) : null
        };
    }

    private static OddsDto MapToOddsDto(OddsValue odds)
    {
        return new OddsDto
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
}