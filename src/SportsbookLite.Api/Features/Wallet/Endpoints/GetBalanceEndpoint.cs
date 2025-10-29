using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Wallet.Responses;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Wallet.Endpoints;

public sealed class GetBalanceEndpoint : EndpointWithoutRequest<BalanceResponse>
{
    private readonly IGrainFactory _grainFactory;

    public GetBalanceEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Get("/api/wallet/{userId}/balance");
        AllowAnonymous();
        Throttle(
            hitLimit: 30,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "Get user wallet balance";
            s.Description = "Retrieves the current balance and available balance for the user's wallet";
            s.Params["userId"] = "The unique identifier of the user";
            s.Response(200, "Balance retrieved successfully", example: new BalanceResponse
            {
                Amount = 1000.00m,
                Currency = "USD",
                AvailableAmount = 950.00m,
                UserId = "user_123",
                Timestamp = DateTimeOffset.UtcNow
            });
            s.Response(404, "User wallet not found");
            s.Response(500, "Failed to retrieve balance");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var userId = Route<string>("userId");
            if (string.IsNullOrEmpty(userId))
            {
                HttpContext.Response.StatusCode = 400;
                return;
            }

            var walletGrain = _grainFactory.GetGrain<IUserWalletGrain>(userId);
            
            var balance = await walletGrain.GetBalanceAsync();
            var availableBalance = await walletGrain.GetAvailableBalanceAsync();

            Response = new BalanceResponse
            {
                Amount = balance.Amount,
                Currency = balance.Currency,
                AvailableAmount = availableBalance.Amount,
                UserId = userId,
                Timestamp = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get balance for user {UserId}", Route<string>("userId"));
            
            HttpContext.Response.StatusCode = 500;
        }
    }
}