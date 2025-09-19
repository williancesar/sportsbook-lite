using FluentAssertions;
using SportsbookLite.Contracts.Betting;
using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.UnitTests.Features.Betting;

public class BetTests
{
    [Fact]
    public void PotentialPayout_ShouldCalculateCorrectly()
    {
        var amount = Money.Create(100m);
        var odds = 2.5m;
        var bet = CreateBasicBet(amount: amount, odds: odds);

        bet.PotentialPayout.Should().Be(Money.Create(250m));
    }

    [Fact]
    public void PotentialPayout_WithDifferentCurrency_ShouldPreserveCurrency()
    {
        var amount = Money.Create(50m, "EUR");
        var odds = 1.8m;
        var bet = CreateBasicBet(amount: amount, odds: odds);

        bet.PotentialPayout.Should().Be(Money.Create(90m, "EUR"));
    }

    [Theory]
    [InlineData(BetStatus.Won, true)]
    [InlineData(BetStatus.Lost, true)]
    [InlineData(BetStatus.Void, true)]
    [InlineData(BetStatus.Pending, false)]
    [InlineData(BetStatus.Accepted, false)]
    [InlineData(BetStatus.Rejected, false)]
    [InlineData(BetStatus.CashOut, false)]
    public void IsSettled_ShouldReturnCorrectValue(BetStatus status, bool expected)
    {
        var bet = CreateBasicBet(status: status);

        bet.IsSettled.Should().Be(expected);
    }

    [Theory]
    [InlineData(BetStatus.Accepted, true)]
    [InlineData(BetStatus.Pending, true)]
    [InlineData(BetStatus.Won, false)]
    [InlineData(BetStatus.Lost, false)]
    [InlineData(BetStatus.Void, false)]
    [InlineData(BetStatus.Rejected, false)]
    [InlineData(BetStatus.CashOut, false)]
    public void CanBeVoided_ShouldReturnCorrectValue(BetStatus status, bool expected)
    {
        var bet = CreateBasicBet(status: status);

        bet.CanBeVoided.Should().Be(expected);
    }

    [Theory]
    [InlineData(BetStatus.Accepted, true)]
    [InlineData(BetStatus.Pending, false)]
    [InlineData(BetStatus.Won, false)]
    [InlineData(BetStatus.Lost, false)]
    [InlineData(BetStatus.Void, false)]
    [InlineData(BetStatus.Rejected, false)]
    [InlineData(BetStatus.CashOut, false)]
    public void CanBeCashedOut_ShouldReturnCorrectValue(BetStatus status, bool expected)
    {
        var bet = CreateBasicBet(status: status);

        bet.CanBeCashedOut.Should().Be(expected);
    }

    [Fact]
    public void Constructor_WithValidData_ShouldCreateBet()
    {
        var betId = Guid.NewGuid();
        var userId = "user123";
        var eventId = Guid.NewGuid();
        var marketId = "market456";
        var selectionId = "selection789";
        var amount = Money.Create(100m);
        var odds = 2.0m;
        var status = BetStatus.Accepted;
        var type = BetType.Single;
        var placedAt = DateTimeOffset.UtcNow;

        var bet = new Bet(
            betId, userId, eventId, marketId, selectionId,
            amount, odds, status, type, placedAt,
            null, null, null, null);

        bet.Id.Should().Be(betId);
        bet.UserId.Should().Be(userId);
        bet.EventId.Should().Be(eventId);
        bet.MarketId.Should().Be(marketId);
        bet.SelectionId.Should().Be(selectionId);
        bet.Amount.Should().Be(amount);
        bet.Odds.Should().Be(odds);
        bet.Status.Should().Be(status);
        bet.Type.Should().Be(type);
        bet.PlacedAt.Should().Be(placedAt);
        bet.SettledAt.Should().BeNull();
        bet.Payout.Should().BeNull();
        bet.RejectionReason.Should().BeNull();
        bet.VoidReason.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithSettledData_ShouldCreateSettledBet()
    {
        var betId = Guid.NewGuid();
        var userId = "user123";
        var eventId = Guid.NewGuid();
        var amount = Money.Create(50m);
        var payout = Money.Create(100m);
        var placedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var settledAt = DateTimeOffset.UtcNow;

        var bet = new Bet(
            betId, userId, eventId, "market", "selection",
            amount, 2.0m, BetStatus.Won, BetType.Single, placedAt,
            settledAt, payout, null, null);

        bet.Status.Should().Be(BetStatus.Won);
        bet.SettledAt.Should().Be(settledAt);
        bet.Payout.Should().Be(payout);
        bet.IsSettled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithRejectedData_ShouldCreateRejectedBet()
    {
        var betId = Guid.NewGuid();
        var rejectionReason = "Insufficient balance";

        var bet = new Bet(
            betId, "user123", Guid.NewGuid(), "market", "selection",
            Money.Create(100m), 1.5m, BetStatus.Rejected, BetType.Single,
            DateTimeOffset.UtcNow, null, null, rejectionReason, null);

        bet.Status.Should().Be(BetStatus.Rejected);
        bet.RejectionReason.Should().Be(rejectionReason);
        bet.IsSettled.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithVoidedData_ShouldCreateVoidedBet()
    {
        var betId = Guid.NewGuid();
        var voidReason = "Event cancelled";
        var settledAt = DateTimeOffset.UtcNow;

        var bet = new Bet(
            betId, "user123", Guid.NewGuid(), "market", "selection",
            Money.Create(75m), 3.0m, BetStatus.Void, BetType.Single,
            DateTimeOffset.UtcNow.AddMinutes(-15), settledAt, null, null, voidReason);

        bet.Status.Should().Be(BetStatus.Void);
        bet.VoidReason.Should().Be(voidReason);
        bet.SettledAt.Should().Be(settledAt);
        bet.IsSettled.Should().BeTrue();
        bet.CanBeVoided.Should().BeFalse();
    }

    [Theory]
    [InlineData(100, 1.5, 150)]
    [InlineData(25, 4.0, 100)]
    [InlineData(200, 1.1, 220)]
    [InlineData(1, 10.0, 10)]
    public void PotentialPayout_WithDifferentAmountsAndOdds_ShouldCalculateCorrectly(decimal amount, decimal odds, decimal expectedPayout)
    {
        var bet = CreateBasicBet(amount: Money.Create(amount), odds: odds);

        bet.PotentialPayout.Should().Be(Money.Create(expectedPayout));
    }

    [Fact]
    public void PotentialPayout_WithZeroAmount_ShouldReturnZero()
    {
        var bet = CreateBasicBet(amount: Money.Zero(), odds: 5.0m);

        bet.PotentialPayout.Should().Be(Money.Zero());
    }

    [Fact]
    public void PotentialPayout_WithZeroOdds_ShouldReturnZero()
    {
        var bet = CreateBasicBet(amount: Money.Create(100m), odds: 0m);

        bet.PotentialPayout.Should().Be(Money.Zero());
    }

    private static Bet CreateBasicBet(
        Guid? id = null,
        string userId = "user123",
        Money? amount = null,
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
            Guid.NewGuid(),
            "market123",
            "selection456",
            amount ?? Money.Create(100m),
            odds,
            status,
            type,
            placedAt ?? DateTimeOffset.UtcNow,
            settledAt,
            payout,
            rejectionReason,
            voidReason);
    }
}