using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Betting.Requests;
using SportsbookLite.Api.Features.Betting.Responses;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Betting.Endpoints;

public sealed class VoidBetEndpoint : Endpoint<VoidBetRequest, PlaceBetResponse>
{
    private readonly IGrainFactory _grainFactory;

    public VoidBetEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Post("/api/bets/{betId}/void");
        AllowAnonymous();
        Throttle(
            hitLimit: 10,
            durationSeconds: 60,
            headerName: "X-Void-Rate-Limit");
        Summary(s =>
        {
            s.Summary = "Void a bet";
            s.Description = "Voids an existing bet and refunds the stake to the user's wallet";
            s.Params["betId"] = "The unique identifier of the bet to void";
            s.Response(200, "Bet voided successfully", example: new PlaceBetResponse
            {
                IsSuccess = true,
                BetId = Guid.NewGuid(),
                Status = "Void"
            });
            s.Response(400, "Invalid request or bet cannot be voided");
            s.Response(404, "Bet not found");
            s.Response(500, "Internal server error");
        });
    }

    public override async Task HandleAsync(VoidBetRequest req, CancellationToken ct)
    {
        try
        {
            var betGrain = _grainFactory.GetGrain<IBetGrain>(req.BetId);
            var result = await betGrain.VoidBetAsync(req.Reason);

            if (result.IsSuccess && result.Bet != null)
            {
                Response = new PlaceBetResponse
                {
                    IsSuccess = true,
                    BetId = result.Bet.Id,
                    Status = result.Bet.Status.ToString(),
                    PlacedAt = result.Bet.PlacedAt
                };
            }
            else
            {
                var statusCode = result.Error?.Contains("not found") == true ? 404 : 400;
                HttpContext.Response.StatusCode = statusCode;
                Response = new PlaceBetResponse
                {
                    IsSuccess = false,
                    ErrorMessage = result.Error
                };
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to void bet {BetId}", req.BetId);
            
            HttpContext.Response.StatusCode = 500;
            Response = new PlaceBetResponse
            {
                IsSuccess = false,
                ErrorMessage = "An error occurred while voiding the bet"
            };
        }
    }
}