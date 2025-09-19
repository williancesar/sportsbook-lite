namespace SportsbookLite.JourneyTests.Builders;

public sealed class SportsbookJourneyBuilder
{
    private readonly HttpClient _client;
    private readonly List<Func<JourneyContext, Task>> _steps;
    private readonly ITestOutputHelper? _output;
    private string? _currentUserId;
    private string _currency = "USD";

    public SportsbookJourneyBuilder(HttpClient client, ITestOutputHelper? output = null)
    {
        _client = client;
        _output = output;
        _steps = new List<Func<JourneyContext, Task>>();
    }

    public SportsbookJourneyBuilder CreateUser(string userId)
    {
        _currentUserId = userId;
        _steps.Add(async context =>
        {
            context.RecordStep($"Creating user: {userId}");
            context.Set("userId", userId);
            context.Set("currentUser", userId);
            await Task.CompletedTask;
        });
        return this;
    }

    public SportsbookJourneyBuilder ForUser(string userId)
    {
        _currentUserId = userId;
        _steps.Add(async context =>
        {
            context.RecordStep($"Switching to user: {userId}");
            context.Set("currentUser", userId);
            await Task.CompletedTask;
        });
        return this;
    }

    public SportsbookJourneyBuilder AsAdministrator(string adminId = "admin")
    {
        _currentUserId = adminId;
        _steps.Add(async context =>
        {
            context.RecordStep($"Operating as administrator: {adminId}");
            context.Set("currentUser", adminId);
            context.Set("isAdmin", true);
            await Task.CompletedTask;
        });
        return this;
    }

    public SportsbookJourneyBuilder FundWallet(decimal amount, string? currency = null)
    {
        currency ??= _currency;
        _steps.Add(async context =>
        {
            var userId = context.Get<string>("currentUser");
            context.RecordStep($"Funding wallet for {userId}: {amount} {currency}");

            var request = new
            {
                UserId = userId,
                Amount = amount,
                Currency = currency,
                TransactionId = Guid.NewGuid().ToString()
            };

            var response = await _client.PostAsJsonAsync($"/api/wallet/{userId}/deposit", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<DepositResponse>();
            context.Set($"balance_{userId}", result!.NewBalance);
            context.RecordAssertion($"Wallet funded with {amount} {currency}", true);
        });
        return this;
    }

    public SportsbookJourneyBuilder CreateEvent(string name, SportType sport, DateTimeOffset? startTime = null)
    {
        _steps.Add(async context =>
        {
            context.RecordStep($"Creating event: {name} ({sport})");

            var request = new
            {
                Name = name,
                SportType = sport.ToString(),
                Competition = "Test Competition",
                StartTime = startTime ?? DateTimeOffset.UtcNow.AddHours(2),
                Participants = new Dictionary<string, string>
                {
                    ["home"] = $"{name} Home",
                    ["away"] = $"{name} Away"
                }
            };

            var response = await _client.PostAsJsonAsync("/api/events", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EventResponse>();
            var eventId = result!.Event.Id;
            
            context.Set("eventId", eventId);
            context.Set($"event_{eventId}", result.Event);
            context.RecordAssertion($"Event created: {eventId}", true);
        });
        return this;
    }

    public SportsbookJourneyBuilder AddMarket(string marketType, params string[] selections)
    {
        _steps.Add(async context =>
        {
            var eventId = context.Get<Guid>("eventId");
            context.RecordStep($"Adding market '{marketType}' to event {eventId}");

            var request = new
            {
                EventId = eventId,
                MarketType = marketType,
                Name = marketType.Replace("_", " ").ToUpper(),
                Selections = selections.Select(s => new { Id = s, Name = s }).ToArray()
            };

            var response = await _client.PostAsJsonAsync($"/api/events/{eventId}/markets", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<MarketResponse>();
            var marketId = result!.Market.Id;
            
            context.Set($"market_{marketType}", marketId);
            context.Set("lastMarketId", marketId);
            context.RecordAssertion($"Market added: {marketType}", true);
        });
        return this;
    }

    public SportsbookJourneyBuilder SetOdds(string selection, decimal odds)
    {
        _steps.Add(async context =>
        {
            var marketId = context.Get<string>("lastMarketId");
            context.RecordStep($"Setting odds for {selection}: {odds}");

            var request = new
            {
                MarketId = marketId,
                SelectionId = selection,
                Odds = odds,
                Timestamp = DateTimeOffset.UtcNow
            };

            var response = await _client.PutAsJsonAsync($"/api/odds/{marketId}", request);
            response.EnsureSuccessStatusCode();

            context.Set($"odds_{marketId}_{selection}", odds);
            context.RecordAssertion($"Odds set: {selection} @ {odds}", true);
        });
        return this;
    }

    public SportsbookJourneyBuilder StartEvent()
    {
        _steps.Add(async context =>
        {
            var eventId = context.Get<Guid>("eventId");
            context.RecordStep($"Starting event {eventId}");

            var response = await _client.PostAsync($"/api/events/{eventId}/start", null);
            response.EnsureSuccessStatusCode();

            context.Set($"event_{eventId}_status", "Live");
            context.RecordAssertion("Event started", true);
        });
        return this;
    }

    public SportsbookJourneyBuilder PlaceBet(decimal stake, string selection, decimal? acceptableOdds = null)
    {
        _steps.Add(async context =>
        {
            var userId = context.Get<string>("currentUser");
            var eventId = context.Get<Guid>("eventId");
            var marketId = context.Get<string>("lastMarketId");
            
            context.RecordStep($"Placing bet: {stake} on {selection}");

            var request = new
            {
                UserId = userId,
                EventId = eventId,
                MarketId = marketId,
                SelectionId = selection,
                Stake = stake,
                Currency = _currency,
                AcceptableOdds = acceptableOdds ?? 1.5m,
                IdempotencyKey = Guid.NewGuid().ToString()
            };

            var response = await _client.PostAsJsonAsync("/api/bets", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                context.RecordAssertion($"Bet placement failed", false, error);
                throw new InvalidOperationException($"Failed to place bet: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<PlaceBetResponse>();
            var betId = result!.BetId;
            
            context.Set("lastBetId", betId);
            context.Set($"bet_{betId}", result);
            
            if (!context.TryGet<List<Guid>>("userBets", out var userBets))
            {
                userBets = new List<Guid>();
                context.Set("userBets", userBets);
            }
            userBets.Add(betId);
            
            context.RecordAssertion($"Bet placed: {betId}", true);
        });
        return this;
    }

    public SportsbookJourneyBuilder PlaceBetWithIdempotency(decimal stake, string selection, string idempotencyKey)
    {
        _steps.Add(async context =>
        {
            var userId = context.Get<string>("currentUser");
            var eventId = context.Get<Guid>("eventId");
            var marketId = context.Get<string>("lastMarketId");
            
            context.RecordStep($"Placing bet with idempotency key: {idempotencyKey}");

            var request = new
            {
                UserId = userId,
                EventId = eventId,
                MarketId = marketId,
                SelectionId = selection,
                Stake = stake,
                Currency = _currency,
                AcceptableOdds = 1.5m,
                IdempotencyKey = idempotencyKey
            };

            var response = await _client.PostAsJsonAsync("/api/bets", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<PlaceBetResponse>();
            
            if (!context.TryGet<Guid>($"idempotent_bet_{idempotencyKey}", out var existingBetId))
            {
                context.Set($"idempotent_bet_{idempotencyKey}", result!.BetId);
                context.Set($"idempotent_count_{idempotencyKey}", 1);
            }
            else
            {
                var count = context.Get<int>($"idempotent_count_{idempotencyKey}");
                context.Set($"idempotent_count_{idempotencyKey}", count + 1);
                
                // Verify same bet ID returned
                if (result!.BetId != existingBetId)
                {
                    context.RecordAssertion("Idempotency check", false, 
                        $"Different bet IDs returned: {existingBetId} vs {result.BetId}");
                }
            }
            
            context.Set("lastBetId", result.BetId);
            context.RecordAssertion($"Idempotent bet placed", true);
        });
        return this;
    }

    public SportsbookJourneyBuilder CompleteEvent(string winningSelection)
    {
        _steps.Add(async context =>
        {
            var eventId = context.Get<Guid>("eventId");
            context.RecordStep($"Completing event {eventId} with result: {winningSelection}");

            var request = new
            {
                EventId = eventId,
                Results = new Dictionary<string, string>
                {
                    ["match_result"] = winningSelection
                }
            };

            var response = await _client.PostAsJsonAsync($"/api/events/{eventId}/complete", request);
            response.EnsureSuccessStatusCode();

            context.Set($"event_{eventId}_result", winningSelection);
            context.Set($"event_{eventId}_status", "Completed");
            context.RecordAssertion($"Event completed with result: {winningSelection}", true);
        });
        return this;
    }

    public SportsbookJourneyBuilder WaitForSettlement(int delayMs = 1000)
    {
        _steps.Add(async context =>
        {
            context.RecordStep($"Waiting {delayMs}ms for settlement processing");
            await Task.Delay(delayMs);
        });
        return this;
    }

    public SportsbookJourneyBuilder RequestCashout()
    {
        _steps.Add(async context =>
        {
            var betId = context.Get<Guid>("lastBetId");
            var userId = context.Get<string>("currentUser");
            
            context.RecordStep($"Requesting cashout for bet {betId}");

            var request = new
            {
                BetId = betId,
                UserId = userId
            };

            var response = await _client.PostAsJsonAsync($"/api/bets/{betId}/cashout", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                context.RecordAssertion("Cashout request", false, error);
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<CashOutResponse>();
            context.Set($"cashout_{betId}", result!);
            context.RecordAssertion($"Cashout successful: {result.CashoutAmount} {result.Currency}", true);
        });
        return this;
    }

    public SportsbookJourneyBuilder VerifyBalance(decimal expectedAmount)
    {
        _steps.Add(async context =>
        {
            var userId = context.Get<string>("currentUser");
            context.RecordStep($"Verifying balance for {userId}");

            var response = await _client.GetAsync($"/api/wallet/{userId}/balance");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
            var actualBalance = result!.Amount;

            var success = Math.Abs(actualBalance - expectedAmount) < 0.01m;
            context.RecordAssertion(
                $"Balance check: expected {expectedAmount}, got {actualBalance}",
                success,
                success ? null : $"Balance mismatch: expected {expectedAmount}, actual {actualBalance}"
            );

            if (!success)
            {
                throw new AssertionException($"Balance verification failed: expected {expectedAmount}, got {actualBalance}");
            }
        });
        return this;
    }

    public SportsbookJourneyBuilder VerifyBetStatus(BetStatus expectedStatus)
    {
        _steps.Add(async context =>
        {
            var betId = context.Get<Guid>("lastBetId");
            context.RecordStep($"Verifying bet {betId} status");

            var response = await _client.GetAsync($"/api/bets/{betId}");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<BetResponse>();
            var actualStatus = Enum.Parse<BetStatus>(result!.Status);

            var success = actualStatus == expectedStatus;
            context.RecordAssertion(
                $"Bet status check: expected {expectedStatus}, got {actualStatus}",
                success,
                success ? null : $"Status mismatch"
            );

            if (!success)
            {
                throw new AssertionException($"Bet status verification failed: expected {expectedStatus}, got {actualStatus}");
            }
        });
        return this;
    }

    public SportsbookJourneyBuilder VerifyOnlyOneBetPlaced()
    {
        _steps.Add(async context =>
        {
            context.RecordStep("Verifying only one bet was placed");
            
            var userBets = context.Get<List<Guid>>("userBets");
            var uniqueBets = userBets.Distinct().Count();
            
            var success = uniqueBets == 1;
            context.RecordAssertion(
                $"Single bet verification: {uniqueBets} unique bet(s)",
                success,
                success ? null : $"Expected 1 bet, found {uniqueBets}"
            );
        });
        return this;
    }

    public SportsbookJourneyBuilder Then(Action<JourneyContext> assertion)
    {
        _steps.Add(async context =>
        {
            context.RecordStep("Custom assertion");
            assertion(context);
            await Task.CompletedTask;
        });
        return this;
    }

    public SportsbookJourneyBuilder Delay(int milliseconds)
    {
        _steps.Add(async context =>
        {
            context.RecordStep($"Waiting {milliseconds}ms");
            await Task.Delay(milliseconds);
        });
        return this;
    }

    public async Task<JourneyContext> ExecuteAsync()
    {
        var context = new JourneyContext();

        try
        {
            foreach (var step in _steps)
            {
                await step(context);
            }
        }
        catch (Exception ex)
        {
            _output?.WriteLine($"Journey failed: {ex.Message}");
            _output?.WriteLine(context.GetJourneyReport());
            throw;
        }

        _output?.WriteLine(context.GetJourneyReport());
        return context;
    }

    public SportsbookJourneyBuilder BuildUserJourney()
    {
        // Return a copy for parallel execution
        var builder = new SportsbookJourneyBuilder(_client, _output)
        {
            _currentUserId = _currentUserId,
            _currency = _currency
        };
        builder._steps.AddRange(_steps);
        return builder;
    }
}

// Response DTOs (temporary until actual ones are available)
public record DepositResponse(Money NewBalance);
public record EventResponse(SportEvent Event);
public record MarketResponse(Market Market);
public record PlaceBetResponse(Guid BetId, BetStatus Status, decimal PotentialPayout);
public record CashOutResponse(decimal CashoutAmount, string Currency);
public record BalanceResponse(decimal Amount, string Currency);
public record BetResponse(Guid BetId, string Status, decimal Stake, decimal? Payout);