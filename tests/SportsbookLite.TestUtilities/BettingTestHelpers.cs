using SportsbookLite.Contracts.Betting;
using SportsbookLite.Contracts.Events;
using SportsbookLite.Contracts.Odds;
using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.TestUtilities;

public static class BettingTestHelpers
{
    public static PlaceBetRequest CreateValidPlaceBetRequest(
        Guid? betId = null,
        string userId = "user123",
        Guid? eventId = null,
        string marketId = "market123",
        string selectionId = "selection456",
        decimal amount = 100m,
        string currency = "USD",
        decimal acceptableOdds = 2.0m,
        BetType type = BetType.Single)
    {
        return new PlaceBetRequest(
            betId ?? Guid.NewGuid(),
            userId,
            eventId ?? Guid.NewGuid(),
            marketId,
            selectionId,
            Money.Create(amount, currency),
            acceptableOdds,
            type
        );
    }

    public static Bet CreateTestBet(
        Guid? id = null,
        string userId = "user123",
        Guid? eventId = null,
        string marketId = "market123",
        string selectionId = "selection456",
        decimal amount = 100m,
        string currency = "USD",
        decimal odds = 2.0m,
        BetStatus status = BetStatus.Accepted,
        BetType type = BetType.Single,
        DateTimeOffset? placedAt = null,
        DateTimeOffset? settledAt = null,
        Money? payout = null,
        string? rejectionReason = null,
        string? voidReason = null)
    {
        return new Bet(
            id ?? Guid.NewGuid(),
            userId,
            eventId ?? Guid.NewGuid(),
            marketId,
            selectionId,
            Money.Create(amount, currency),
            odds,
            status,
            type,
            placedAt ?? DateTimeOffset.UtcNow,
            settledAt,
            payout,
            rejectionReason,
            voidReason
        );
    }

    public static BetPlacedEvent CreateBetPlacedEvent(
        Guid? betId = null,
        string userId = "user123",
        Guid? eventId = null,
        string marketId = "market123",
        string selectionId = "selection456",
        decimal amount = 100m,
        decimal acceptableOdds = 2.0m,
        BetType type = BetType.Single,
        DateTimeOffset? timestamp = null)
    {
        var id = betId ?? Guid.NewGuid();
        return new BetPlacedEvent(
            Guid.NewGuid(),
            timestamp ?? DateTimeOffset.UtcNow,
            id.ToString(),
            id,
            userId,
            eventId ?? Guid.NewGuid(),
            marketId,
            selectionId,
            Money.Create(amount),
            acceptableOdds,
            type
        );
    }

    public static BetAcceptedEvent CreateBetAcceptedEvent(
        Guid? betId = null,
        string userId = "user123",
        decimal finalOdds = 2.5m,
        Money? potentialPayout = null,
        DateTimeOffset? timestamp = null)
    {
        var id = betId ?? Guid.NewGuid();
        return new BetAcceptedEvent(
            Guid.NewGuid(),
            timestamp ?? DateTimeOffset.UtcNow,
            id.ToString(),
            id,
            userId,
            finalOdds,
            potentialPayout ?? Money.Create(250m)
        );
    }

    public static BetRejectedEvent CreateBetRejectedEvent(
        Guid? betId = null,
        string userId = "user123",
        string reason = "Test rejection",
        DateTimeOffset? timestamp = null)
    {
        var id = betId ?? Guid.NewGuid();
        return new BetRejectedEvent(
            Guid.NewGuid(),
            timestamp ?? DateTimeOffset.UtcNow,
            id.ToString(),
            id,
            userId,
            reason
        );
    }

    public static BetSettledEvent CreateBetSettledEvent(
        Guid? betId = null,
        string userId = "user123",
        BetStatus finalStatus = BetStatus.Won,
        Money? payout = null,
        DateTimeOffset? timestamp = null)
    {
        var id = betId ?? Guid.NewGuid();
        return new BetSettledEvent(
            Guid.NewGuid(),
            timestamp ?? DateTimeOffset.UtcNow,
            id.ToString(),
            id,
            userId,
            finalStatus,
            payout
        );
    }

    public static BetVoidedEvent CreateBetVoidedEvent(
        Guid? betId = null,
        string userId = "user123",
        string reason = "Event cancelled",
        DateTimeOffset? timestamp = null)
    {
        var id = betId ?? Guid.NewGuid();
        return new BetVoidedEvent(
            Guid.NewGuid(),
            timestamp ?? DateTimeOffset.UtcNow,
            id.ToString(),
            id,
            userId,
            reason
        );
    }

    public static OddsSnapshot CreateOddsSnapshot(
        string marketId = "market123",
        Dictionary<string, Odds>? selections = null,
        DateTimeOffset? timestamp = null,
        OddsVolatility volatility = OddsVolatility.Low)
    {
        var defaultSelections = new Dictionary<string, Odds>
        {
            ["selection456"] = new Odds(
                2.5m,
                marketId,
                "selection456",
                OddsSource.Manual,
                timestamp ?? DateTimeOffset.UtcNow
            ),
            ["selection789"] = new Odds(
                1.8m,
                marketId,
                "selection789",
                OddsSource.Manual,
                timestamp ?? DateTimeOffset.UtcNow
            )
        };

        return new OddsSnapshot(
            marketId,
            selections ?? defaultSelections,
            timestamp ?? DateTimeOffset.UtcNow,
            volatility
        );
    }

    public static List<IDomainEvent> CreateBetEventSequence(
        Guid betId,
        string userId = "user123",
        BetStatus finalStatus = BetStatus.Won)
    {
        var events = new List<IDomainEvent>();
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-30);

        events.Add(CreateBetPlacedEvent(
            betId, userId, timestamp: baseTime));

        if (finalStatus != BetStatus.Pending && finalStatus != BetStatus.Rejected)
        {
            events.Add(CreateBetAcceptedEvent(
                betId, userId, timestamp: baseTime.AddMinutes(1)));

            if (finalStatus == BetStatus.Won || finalStatus == BetStatus.Lost)
            {
                events.Add(CreateBetSettledEvent(
                    betId, userId, finalStatus, 
                    finalStatus == BetStatus.Won ? Money.Create(250m) : null,
                    timestamp: baseTime.AddMinutes(20)));
            }
            else if (finalStatus == BetStatus.Void)
            {
                events.Add(CreateBetVoidedEvent(
                    betId, userId, timestamp: baseTime.AddMinutes(10)));
            }
        }
        else if (finalStatus == BetStatus.Rejected)
        {
            events.Add(CreateBetRejectedEvent(
                betId, userId, timestamp: baseTime.AddMinutes(1)));
        }

        return events;
    }

    public static BetSlip CreateBetSlip(
        string userId = "user123",
        BetType type = BetType.Single,
        Money? totalStake = null,
        params BetSelection[] selections)
    {
        var selectionsList = selections.Any() 
            ? selections.ToList()
            : new List<BetSelection>
            {
                new BetSelection(
                    Guid.NewGuid(),
                    "market123",
                    "selection456", 
                    2.0m,
                    "Test Selection")
            };

        return new BetSlip(
            Guid.NewGuid(),
            userId,
            selectionsList,
            totalStake ?? Money.Create(100m),
            type,
            DateTimeOffset.UtcNow
        );
    }

    public static TransactionResult CreateSuccessfulTransactionResult(
        Money? newBalance = null,
        WalletTransaction? transaction = null)
    {
        return TransactionResult.Success(
            transaction ?? CreateWalletTransaction(),
            newBalance ?? Money.Create(1000m)
        );
    }

    public static WalletTransaction CreateWalletTransaction(
        string? id = null,
        string userId = "user123",
        TransactionType type = TransactionType.Deposit,
        decimal amount = 100m,
        string currency = "USD",
        TransactionStatus status = TransactionStatus.Completed,
        string? referenceId = null)
    {
        return new WalletTransaction(
            id ?? Guid.NewGuid().ToString(),
            userId,
            type,
            Money.Create(amount, currency),
            status,
            $"{type} transaction",
            DateTimeOffset.UtcNow,
            referenceId
        );
    }

    public static class BetAmounts
    {
        public static readonly Money Small = Money.Create(10m);
        public static readonly Money Medium = Money.Create(100m);
        public static readonly Money Large = Money.Create(1000m);
        public static readonly Money Maximum = Money.Create(10000m);
    }

    public static class TestOdds
    {
        public static readonly decimal Favorite = 1.5m;
        public static readonly decimal Even = 2.0m;
        public static readonly decimal Underdog = 3.5m;
        public static readonly decimal LongShot = 10.0m;
    }

    public static class TestUsers
    {
        public static readonly string User1 = "user001";
        public static readonly string User2 = "user002";
        public static readonly string User3 = "user003";
        public static readonly string TestUser = "testuser";
        public static readonly string AdminUser = "admin";
    }

    public static class TestMarkets
    {
        public static readonly string Match1Winner = "match1-winner";
        public static readonly string Match1OverUnder = "match1-over-under";
        public static readonly string Match2Winner = "match2-winner";
        public static readonly string TournamentWinner = "tournament-winner";
    }

    public static class TestSelections
    {
        public static readonly string Team1Win = "team1-win";
        public static readonly string Team2Win = "team2-win";
        public static readonly string Draw = "draw";
        public static readonly string Over25 = "over-2.5";
        public static readonly string Under25 = "under-2.5";
    }
}