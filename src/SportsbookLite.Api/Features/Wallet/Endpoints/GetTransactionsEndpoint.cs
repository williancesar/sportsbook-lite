using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Wallet.Requests;
using SportsbookLite.Api.Features.Wallet.Responses;
using SportsbookLite.Contracts.Wallet;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Wallet.Endpoints;

public sealed class GetTransactionsEndpoint : Endpoint<GetTransactionsRequest, TransactionsResponse>
{
    private readonly IGrainFactory _grainFactory;

    public GetTransactionsEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Get("/api/wallet/{userId}/transactions");
        AllowAnonymous();
        Throttle(
            hitLimit: 20,
            durationSeconds: 60,
            headerName: "X-Throttle-Limit");
        Summary(s =>
        {
            s.Summary = "Get user wallet transaction history";
            s.Description = "Retrieves the transaction history for the user's wallet with optional pagination";
            s.Params["userId"] = "The unique identifier of the user";
            s.RequestParam(r => r.Limit, "Maximum number of transactions to return (1-1000, default: 50)");
            s.RequestParam(r => r.Offset, "Number of transactions to skip for pagination (default: 0)");
            s.Response(200, "Transactions retrieved successfully", example: new TransactionsResponse
            {
                Transactions = new[]
                {
                    new TransactionResponseDto
                    {
                        Id = "txn_123456789",
                        UserId = "user_123",
                        Type = "Deposit",
                        Amount = 100.00m,
                        Currency = "USD",
                        Status = "Completed",
                        Description = "Initial deposit",
                        Timestamp = DateTimeOffset.UtcNow.AddDays(-1)
                    }
                },
                Count = 1,
                Offset = 0,
                Limit = 50,
                HasMore = false
            });
            s.Response(400, "Invalid request parameters");
            s.Response(404, "User wallet not found");
            s.Response(500, "Failed to retrieve transactions");
        });
    }

    public override async Task HandleAsync(GetTransactionsRequest req, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrEmpty(req.UserId))
            {
                HttpContext.Response.StatusCode = 400;
                return;
            }

            var walletGrain = _grainFactory.GetGrain<IUserWalletGrain>(req.UserId);
            
            var transactions = await walletGrain.GetTransactionHistoryAsync(req.Limit + 1);

            var transactionList = transactions
                .Skip(req.Offset)
                .Take(req.Limit)
                .Select(MapTransaction)
                .ToList();

            var hasMore = transactions.Count > req.Offset + req.Limit;

            Response = new TransactionsResponse
            {
                Transactions = transactionList,
                Count = transactionList.Count,
                Offset = req.Offset,
                Limit = req.Limit,
                HasMore = hasMore
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get transactions for user {UserId}", req.UserId);
            
            HttpContext.Response.StatusCode = 500;
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
}