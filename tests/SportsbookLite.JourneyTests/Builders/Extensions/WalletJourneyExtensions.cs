namespace SportsbookLite.JourneyTests.Builders.Extensions;

public static class WalletJourneyExtensions
{
    public static SportsbookJourneyBuilder Withdraw(this SportsbookJourneyBuilder builder, decimal amount, string currency = "USD")
    {
        return builder.Then(async context =>
        {
            var userId = context.Get<string>("currentUser");
            context.RecordStep($"Withdrawing {amount} {currency} from wallet");

            var client = context.Get<HttpClient>("httpClient");
            var request = new
            {
                UserId = userId,
                Amount = amount,
                Currency = currency,
                TransactionId = Guid.NewGuid().ToString()
            };

            var response = await client.PostAsJsonAsync($"/api/wallet/{userId}/withdraw", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<WithdrawResponse>();
            context.Set($"balance_{userId}", result!.NewBalance);
            context.RecordAssertion($"Withdrawal of {amount} {currency} successful", true);
        });
    }

    public static SportsbookJourneyBuilder GetTransactionHistory(this SportsbookJourneyBuilder builder)
    {
        return builder.Then(async context =>
        {
            var userId = context.Get<string>("currentUser");
            context.RecordStep($"Getting transaction history for {userId}");

            var client = context.Get<HttpClient>("httpClient");
            var response = await client.GetAsync($"/api/wallet/{userId}/transactions");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TransactionsResponse>();
            context.Set($"transactions_{userId}", result!.Transactions);
            context.RecordAssertion($"Retrieved {result.Transactions.Count} transactions", true);
        });
    }

    public static SportsbookJourneyBuilder VerifyTransactionCount(this SportsbookJourneyBuilder builder, int expectedCount)
    {
        return builder.Then(context =>
        {
            var userId = context.Get<string>("currentUser");
            var transactions = context.Get<List<WalletTransaction>>($"transactions_{userId}");
            
            var success = transactions.Count == expectedCount;
            context.RecordAssertion(
                $"Transaction count: expected {expectedCount}, got {transactions.Count}",
                success,
                success ? null : $"Count mismatch"
            );
        });
    }

    public static SportsbookJourneyBuilder VerifyBalanceIncreased(this SportsbookJourneyBuilder builder)
    {
        return builder.Then(async context =>
        {
            var userId = context.Get<string>("currentUser");
            var previousBalance = context.Get<Money>($"balance_{userId}_previous");
            
            context.RecordStep($"Verifying balance increased for {userId}");

            var client = context.Get<HttpClient>("httpClient");
            var response = await client.GetAsync($"/api/wallet/{userId}/balance");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
            var currentBalance = result!.Amount;

            var success = currentBalance > previousBalance.Amount;
            context.RecordAssertion(
                $"Balance increased from {previousBalance.Amount} to {currentBalance}",
                success,
                success ? null : "Balance did not increase"
            );
        });
    }

    public static SportsbookJourneyBuilder SaveCurrentBalance(this SportsbookJourneyBuilder builder)
    {
        return builder.Then(async context =>
        {
            var userId = context.Get<string>("currentUser");
            context.RecordStep($"Saving current balance for {userId}");

            var client = context.Get<HttpClient>("httpClient");
            var response = await client.GetAsync($"/api/wallet/{userId}/balance");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
            context.Set($"balance_{userId}_previous", new Money(result!.Amount, result.Currency));
        });
    }

    public static SportsbookJourneyBuilder VerifyInsufficientFunds(this SportsbookJourneyBuilder builder, decimal attemptAmount)
    {
        return builder.Then(async context =>
        {
            var userId = context.Get<string>("currentUser");
            context.RecordStep($"Verifying insufficient funds rejection for {attemptAmount}");

            var client = context.Get<HttpClient>("httpClient");
            var request = new
            {
                UserId = userId,
                Amount = attemptAmount,
                Currency = "USD",
                TransactionId = Guid.NewGuid().ToString()
            };

            var response = await client.PostAsJsonAsync($"/api/wallet/{userId}/withdraw", request);
            
            var success = !response.IsSuccessStatusCode;
            context.RecordAssertion(
                "Insufficient funds check",
                success,
                success ? null : "Withdrawal should have been rejected"
            );
        });
    }
}

// Response DTOs
public record WithdrawResponse(Money NewBalance);
public record TransactionsResponse(List<WalletTransaction> Transactions);