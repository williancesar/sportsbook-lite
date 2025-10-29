using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Betting.Requests;
using SportsbookLite.Api.Features.Betting.Responses;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Betting.Endpoints;

public sealed class CashOutEndpoint : Endpoint<CashOutRequest, CashOutResponse>
{
    private readonly IGrainFactory _grainFactory;

    public CashOutEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Post("/api/bets/{betId}/cashout");
        AllowAnonymous();
        Throttle(
            hitLimit: 10,
            durationSeconds: 60,
            headerName: "X-CashOut-Rate-Limit");
        Summary(s =>
        {
            s.Summary = "Cash out a bet";
            s.Description = "Cashes out an active bet at the current market value";
            s.Params["betId"] = "The unique identifier of the bet to cash out";
            s.Response(200, "Bet cashed out successfully", example: new CashOutResponse
            {
                IsSuccess = true,
                PayoutAmount = 75.50m,
                Currency = "USD",
                Fees = 2.50m,
                CashedOutAt = DateTimeOffset.UtcNow
            });
            s.Response(400, "Invalid request or bet cannot be cashed out");
            s.Response(404, "Bet not found");
            s.Response(500, "Internal server error");
        });
    }

    public override async Task HandleAsync(CashOutRequest req, CancellationToken ct)
    {
        try
        {
            var betGrain = _grainFactory.GetGrain<IBetGrain>(req.BetId);
            var result = await betGrain.CashOutAsync();

            if (result.IsSuccess && result.Bet != null)
            {
                var payout = result.Bet.Payout?.Amount ?? 0;
                var fees = payout * 0.05m;
                var netPayout = payout - fees;

                Response = new CashOutResponse
                {
                    IsSuccess = true,
                    PayoutAmount = netPayout,
                    Currency = result.Bet.Amount.Currency,
                    Fees = fees,
                    CashedOutAt = DateTimeOffset.UtcNow
                };
            }
            else
            {
                var statusCode = result.Error?.Contains("not found") == true ? 404 : 400;
                HttpContext.Response.StatusCode = statusCode;
                Response = new CashOutResponse
                {
                    IsSuccess = false,
                    ErrorMessage = result.Error
                };
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to cash out bet {BetId}", req.BetId);
            
            HttpContext.Response.StatusCode = 500;
            Response = new CashOutResponse
            {
                IsSuccess = false,
                ErrorMessage = "An error occurred while cashing out the bet"
            };
        }
    }
}