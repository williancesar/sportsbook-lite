using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Betting.Requests;
using SportsbookLite.Api.Features.Betting.Responses;
using SportsbookLite.Contracts.Betting;
using SportsbookLite.Contracts.Wallet;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Betting.Endpoints;

public sealed class PlaceBetEndpoint : Endpoint<PlaceBetApiRequest, PlaceBetResponse>
{
    private readonly IGrainFactory _grainFactory;

    public PlaceBetEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Post("/api/bets");
        AllowAnonymous();
        Throttle(
            hitLimit: 5,
            durationSeconds: 60,
            headerName: "X-Betting-Rate-Limit");
        Summary(s =>
        {
            s.Summary = "Place a new bet";
            s.Description = "Places a bet on a specific selection with odds validation and balance checks";
            s.Response(201, "Bet placed successfully", example: new PlaceBetResponse
            {
                IsSuccess = true,
                BetId = Guid.NewGuid(),
                Status = "Accepted",
                PotentialPayout = 150.00m,
                Currency = "USD",
                ActualOdds = 1.5m,
                PlacedAt = DateTimeOffset.UtcNow
            });
            s.Response(400, "Invalid request or insufficient funds");
            s.Response(409, "Odds no longer available");
            s.Response(500, "Internal server error");
        });
    }

    public override async Task HandleAsync(PlaceBetApiRequest req, CancellationToken ct)
    {
        try
        {
            var betId = !string.IsNullOrEmpty(req.IdempotencyKey) 
                ? GenerateIdempotentBetId(req.IdempotencyKey) 
                : Guid.NewGuid();

            var betGrain = _grainFactory.GetGrain<IBetGrain>(betId);
            var betManagerGrain = _grainFactory.GetGrain<IBetManagerGrain>(req.UserId);

            if (!string.IsNullOrEmpty(req.IdempotencyKey) && await betManagerGrain.HasBetAsync(betId))
            {
                var existingBet = await betGrain.GetBetDetailsAsync();
                if (existingBet != null)
                {
                    HttpContext.Response.StatusCode = 200;
                    Response = MapToPlaceBetResponse(existingBet, true);
                    return;
                }
            }

            var placeBetRequest = new PlaceBetRequest(
                betId,
                req.UserId,
                req.EventId,
                req.MarketId,
                req.SelectionId,
                Money.Create(req.Stake, req.Currency),
                req.AcceptableOdds);

            var result = await betGrain.PlaceBetAsync(placeBetRequest);

            if (result.IsSuccess && result.Bet != null)
            {
                await betManagerGrain.AddBetAsync(betId);
                HttpContext.Response.StatusCode = 201;
                Response = MapToPlaceBetResponse(result.Bet, false);
            }
            else
            {
                var statusCode = result.Error?.Contains("odds") == true ? 409 : 400;
                HttpContext.Response.StatusCode = statusCode;
                Response = new PlaceBetResponse
                {
                    IsSuccess = false,
                    ErrorMessage = result.Error
                };
            }
        }
        catch (ArgumentException ex)
        {
            HttpContext.Response.StatusCode = 400;
            Response = new PlaceBetResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to place bet for user {UserId}", req.UserId);
            
            HttpContext.Response.StatusCode = 500;
            Response = new PlaceBetResponse
            {
                IsSuccess = false,
                ErrorMessage = "An error occurred while placing the bet"
            };
        }
    }

    private static PlaceBetResponse MapToPlaceBetResponse(Bet bet, bool isIdempotent) =>
        new()
        {
            IsSuccess = true,
            BetId = bet.Id,
            Status = bet.Status.ToString(),
            PotentialPayout = bet.PotentialPayout.Amount,
            Currency = bet.Amount.Currency,
            ActualOdds = bet.Odds,
            PlacedAt = bet.PlacedAt
        };

    private static Guid GenerateIdempotentBetId(string idempotencyKey)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(idempotencyKey));
        return new Guid(hash[0..16]);
    }
}