using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Wallet.Requests;
using SportsbookLite.Api.Features.Wallet.Responses;
using SportsbookLite.Contracts.Wallet;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Wallet.Endpoints;

public sealed class WithdrawEndpoint : Endpoint<WithdrawRequest, WithdrawResponse>
{
    private readonly IGrainFactory _grainFactory;

    public WithdrawEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Post("/api/wallet/{userId}/withdraw");
        AllowAnonymous();
        Throttle(
            hitLimit: 5,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "Withdraw funds from user wallet";
            s.Description = "Withdraws the specified amount from the user's wallet using double-entry bookkeeping";
            s.Params["userId"] = "The unique identifier of the user";
            s.Response(200, "Withdrawal successful", example: new WithdrawResponse
            {
                IsSuccess = true,
                Transaction = new TransactionResponseDto
                {
                    Id = "txn_987654321",
                    UserId = "user_123",
                    Type = "Withdrawal",
                    Amount = 50.00m,
                    Currency = "USD",
                    Status = "Completed",
                    Description = "Wallet withdrawal",
                    Timestamp = DateTimeOffset.UtcNow
                },
                NewBalance = new MoneyResponseDto
                {
                    Amount = 950.00m,
                    Currency = "USD"
                }
            });
            s.Response(400, "Invalid request or insufficient funds");
            s.Response(500, "Transaction failed");
        });
    }

    public override async Task HandleAsync(WithdrawRequest req, CancellationToken ct)
    {
        try
        {
            var walletGrain = _grainFactory.GetGrain<IUserWalletGrain>(req.UserId);
            var money = Money.Create(req.Amount, req.Currency);
            
            var result = await walletGrain.WithdrawAsync(money, req.TransactionId);

            if (result.IsSuccess)
            {
                Response = new WithdrawResponse
                {
                    IsSuccess = true,
                    Transaction = result.Transaction != null ? MapTransaction(result.Transaction.Value) : null,
                    NewBalance = result.NewBalance != null ? MapMoney(result.NewBalance.Value) : null
                };
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
                Response = new WithdrawResponse
                {
                    IsSuccess = false,
                    ErrorMessage = result.ErrorMessage
                };
            }
        }
        catch (ArgumentException ex)
        {
            HttpContext.Response.StatusCode = 400;
            Response = new WithdrawResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process withdrawal for user {UserId}", req.UserId);
            
            HttpContext.Response.StatusCode = 500;
            Response = new WithdrawResponse
            {
                IsSuccess = false,
                ErrorMessage = "An error occurred while processing the withdrawal"
            };
        }
    }

    private static TransactionResponseDto MapTransaction(WalletTransaction transaction) =>
        new()
        {
            Id = transaction.Id,
            UserId = transaction.UserId,
            Type = transaction.Type.ToString(),
            Amount = transaction.Amount.Amount,
            Currency = transaction.Amount.Currency,
            Status = transaction.Status.ToString(),
            Description = transaction.Description,
            Timestamp = transaction.Timestamp,
            ReferenceId = transaction.ReferenceId,
            ErrorMessage = transaction.ErrorMessage
        };

    private static MoneyResponseDto MapMoney(Money money) =>
        new()
        {
            Amount = money.Amount,
            Currency = money.Currency
        };
}