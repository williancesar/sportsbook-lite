using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Betting.Requests;
using SportsbookLite.Api.Features.Betting.Responses;
using SportsbookLite.Contracts.Betting;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Betting.Endpoints;

public sealed class GetUserBetsEndpoint : Endpoint<GetUserBetsRequest, UserBetsResponse>
{
    private readonly IGrainFactory _grainFactory;

    public GetUserBetsEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Get("/api/users/{userId}/bets");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get user bets";
            s.Description = "Retrieves paginated list of bets for a specific user with optional filtering";
            s.Params["userId"] = "The unique identifier of the user";
            s.Response(200, "Bets retrieved successfully", example: new UserBetsResponse
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
                PageSize = 20,
                HasNextPage = false
            });
            s.Response(400, "Invalid request parameters");
            s.Response(500, "Internal server error");
        });
    }

    public override async Task HandleAsync(GetUserBetsRequest req, CancellationToken ct)
    {
        try
        {
            var betManagerGrain = _grainFactory.GetGrain<IBetManagerGrain>(req.UserId);
            var allBets = await betManagerGrain.GetUserBetsAsync(1000);

            var filteredBets = FilterBets(allBets, req);
            var paginatedBets = PaginateBets(filteredBets, req.Page, req.PageSize);

            Response = new UserBetsResponse
            {
                Bets = paginatedBets.Select(MapToBetSummaryDto).ToArray(),
                TotalCount = filteredBets.Count,
                Page = req.Page,
                PageSize = req.PageSize,
                HasNextPage = (req.Page * req.PageSize) < filteredBets.Count
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get bets for user {UserId}", req.UserId);
            
            HttpContext.Response.StatusCode = 500;
            Response = new UserBetsResponse();
        }
    }

    private static IReadOnlyList<Bet> FilterBets(IReadOnlyList<Bet> bets, GetUserBetsRequest request)
    {
        var filtered = bets.AsEnumerable();

        if (!string.IsNullOrEmpty(request.Status) && 
            Enum.TryParse<BetStatus>(request.Status, true, out var status))
        {
            filtered = filtered.Where(b => b.Status == status);
        }

        if (request.FromDate.HasValue)
        {
            filtered = filtered.Where(b => b.PlacedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            filtered = filtered.Where(b => b.PlacedAt <= request.ToDate.Value);
        }

        return filtered.OrderByDescending(b => b.PlacedAt).ToArray();
    }

    private static IReadOnlyList<Bet> PaginateBets(IReadOnlyList<Bet> bets, int page, int pageSize)
    {
        var skip = (page - 1) * pageSize;
        return bets.Skip(skip).Take(pageSize).ToArray();
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