using FluentAssertions;
using SportsbookLite.Contracts.Events;
using SportsbookLite.TestUtilities;

namespace SportsbookLite.UnitTests.Features.Events;

public class MarketTests : BaseUnitTest
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        var eventId = Guid.NewGuid();
        var name = "Match Winner";
        var description = "Select the winner of the match";
        var outcomes = new Dictionary<string, decimal>
        {
            { "home", 1.85m },
            { "away", 2.10m },
            { "draw", 3.40m }
        };

        var market = Market.Create(eventId, name, description, outcomes);

        market.Id.Should().NotBeEmpty();
        market.EventId.Should().Be(eventId);
        market.Name.Should().Be(name);
        market.Description.Should().Be(description);
        market.Status.Should().Be(MarketStatus.Open);
        market.Outcomes.Should().BeEquivalentTo(outcomes);
        market.WinningOutcome.Should().BeNull();
        market.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        market.LastModified.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void WithStatus_ShouldUpdateStatusAndModified()
    {
        var market = CreateTestMarket();

        var updatedMarket = market.WithStatus(MarketStatus.Suspended);

        updatedMarket.Status.Should().Be(MarketStatus.Suspended);
        updatedMarket.Id.Should().Be(market.Id);
        updatedMarket.Name.Should().Be(market.Name);
        updatedMarket.LastModified.Should().BeAfter(market.LastModified);
        updatedMarket.WinningOutcome.Should().BeNull();
    }

    [Fact]
    public void WithOutcomes_ShouldUpdateOutcomesAndModified()
    {
        var market = CreateTestMarket();
        var newOutcomes = new Dictionary<string, decimal>
        {
            { "home", 2.00m },
            { "away", 1.95m },
            { "draw", 3.20m }
        };

        var updatedMarket = market.WithOutcomes(newOutcomes);

        updatedMarket.Outcomes.Should().BeEquivalentTo(newOutcomes);
        updatedMarket.Id.Should().Be(market.Id);
        updatedMarket.Status.Should().Be(market.Status);
        updatedMarket.LastModified.Should().BeAfter(market.LastModified);
    }

    [Fact]
    public void WithWinner_ShouldSetWinnerAndMarkAsSettled()
    {
        var market = CreateTestMarket().WithStatus(MarketStatus.Closed);
        var winningOutcome = "home";

        var updatedMarket = market.WithWinner(winningOutcome);

        updatedMarket.WinningOutcome.Should().Be(winningOutcome);
        updatedMarket.Status.Should().Be(MarketStatus.Settled);
        updatedMarket.Id.Should().Be(market.Id);
        updatedMarket.LastModified.Should().BeAfter(market.LastModified);
    }

    [Theory]
    [InlineData(MarketStatus.Open, MarketStatus.Suspended, true)]
    [InlineData(MarketStatus.Open, MarketStatus.Closed, true)]
    [InlineData(MarketStatus.Suspended, MarketStatus.Open, true)]
    [InlineData(MarketStatus.Suspended, MarketStatus.Closed, true)]
    [InlineData(MarketStatus.Closed, MarketStatus.Settled, true)]
    public void CanTransitionTo_ValidTransitions_ShouldReturnTrue(MarketStatus fromStatus, MarketStatus toStatus, bool expected)
    {
        var market = CreateTestMarket().WithStatus(fromStatus);

        var result = market.CanTransitionTo(toStatus);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(MarketStatus.Open, MarketStatus.Settled)]
    [InlineData(MarketStatus.Suspended, MarketStatus.Settled)]
    [InlineData(MarketStatus.Closed, MarketStatus.Open)]
    [InlineData(MarketStatus.Closed, MarketStatus.Suspended)]
    [InlineData(MarketStatus.Settled, MarketStatus.Open)]
    [InlineData(MarketStatus.Settled, MarketStatus.Suspended)]
    [InlineData(MarketStatus.Settled, MarketStatus.Closed)]
    public void CanTransitionTo_InvalidTransitions_ShouldReturnFalse(MarketStatus fromStatus, MarketStatus toStatus)
    {
        var market = CreateTestMarket().WithStatus(fromStatus);

        var result = market.CanTransitionTo(toStatus);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanTransitionTo_SameStatus_ShouldReturnFalse()
    {
        var market = CreateTestMarket();

        var result = market.CanTransitionTo(MarketStatus.Open);

        result.Should().BeFalse();
    }

    [Fact]
    public void Create_DefaultsToOpenStatus_Always()
    {
        var market = CreateTestMarket();

        market.Status.Should().Be(MarketStatus.Open);
    }

    [Fact]
    public void WithStatus_ChainedCalls_ShouldWorkCorrectly()
    {
        var market = CreateTestMarket();

        var finalMarket = market
            .WithStatus(MarketStatus.Suspended)
            .WithStatus(MarketStatus.Closed);

        finalMarket.Status.Should().Be(MarketStatus.Closed);
        finalMarket.LastModified.Should().BeAfter(market.LastModified);
    }

    [Fact]
    public void WithOutcomes_EmptyOutcomes_ShouldSucceed()
    {
        var market = CreateTestMarket();
        var emptyOutcomes = new Dictionary<string, decimal>();

        var updatedMarket = market.WithOutcomes(emptyOutcomes);

        updatedMarket.Outcomes.Should().BeEmpty();
        updatedMarket.Id.Should().Be(market.Id);
    }

    [Fact]
    public void WithOutcomes_SingleOutcome_ShouldSucceed()
    {
        var market = CreateTestMarket();
        var singleOutcome = new Dictionary<string, decimal> { { "winner", 1.01m } };

        var updatedMarket = market.WithOutcomes(singleOutcome);

        updatedMarket.Outcomes.Should().HaveCount(1);
        updatedMarket.Outcomes.Should().ContainKey("winner");
        updatedMarket.Outcomes["winner"].Should().Be(1.01m);
    }

    [Fact]
    public void Equality_WithSameValues_ShouldBeEqual()
    {
        var eventId = Guid.NewGuid();
        var marketId = Guid.NewGuid();
        var outcomes = new Dictionary<string, decimal> { { "home", 1.85m }, { "away", 2.10m } };
        var createdAt = DateTimeOffset.UtcNow;

        var market1 = new Market(marketId, eventId, "Test Market", "Description", MarketStatus.Open, outcomes, createdAt, createdAt);
        var market2 = new Market(marketId, eventId, "Test Market", "Description", MarketStatus.Open, outcomes, createdAt, createdAt);

        market1.Should().Be(market2);
        (market1 == market2).Should().BeTrue();
        (market1 != market2).Should().BeFalse();
    }

    [Fact]
    public void Equality_WithDifferentIds_ShouldNotBeEqual()
    {
        var eventId = Guid.NewGuid();
        var outcomes = new Dictionary<string, decimal> { { "home", 1.85m }, { "away", 2.10m } };
        var createdAt = DateTimeOffset.UtcNow;

        var market1 = new Market(Guid.NewGuid(), eventId, "Test Market", "Description", MarketStatus.Open, outcomes, createdAt, createdAt);
        var market2 = new Market(Guid.NewGuid(), eventId, "Test Market", "Description", MarketStatus.Open, outcomes, createdAt, createdAt);

        market1.Should().NotBe(market2);
        (market1 == market2).Should().BeFalse();
        (market1 != market2).Should().BeTrue();
    }

    [Fact]
    public void Outcomes_Modification_ShouldNotAffectRecord()
    {
        var outcomes = new Dictionary<string, decimal> { { "home", 1.85m }, { "away", 2.10m } };
        var market = CreateTestMarket(outcomes: outcomes);
        var originalOutcomesCount = market.Outcomes.Count;

        var modifiedOutcomes = new Dictionary<string, decimal>(market.Outcomes)
        {
            ["draw"] = 3.40m
        };

        market.Outcomes.Should().HaveCount(originalOutcomesCount);
        market.Outcomes.Should().NotContainKey("draw");
        modifiedOutcomes.Should().ContainKey("draw");
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1.00)]
    [InlineData(999.99)]
    [InlineData(1000000.00)]
    public void Create_WithValidOdds_ShouldSucceed(decimal odds)
    {
        var outcomes = new Dictionary<string, decimal> { { "test", odds } };
        var market = CreateTestMarket(outcomes: outcomes);

        market.Outcomes["test"].Should().Be(odds);
    }

    [Fact]
    public void Create_WithNegativeOdds_ShouldSucceed()
    {
        var outcomes = new Dictionary<string, decimal> { { "test", -1.50m } };
        var market = CreateTestMarket(outcomes: outcomes);

        market.Outcomes["test"].Should().Be(-1.50m);
    }

    [Fact]
    public void Create_WithZeroOdds_ShouldSucceed()
    {
        var outcomes = new Dictionary<string, decimal> { { "test", 0m } };
        var market = CreateTestMarket(outcomes: outcomes);

        market.Outcomes["test"].Should().Be(0m);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("very_long_outcome_name_that_exceeds_normal_expectations")]
    [InlineData("Special-Characters!@#$%^&*()")]
    [InlineData("Unicode_漢字_عربي_русский")]
    public void Create_WithVariousOutcomeNames_ShouldSucceed(string outcomeName)
    {
        var outcomes = new Dictionary<string, decimal> { { outcomeName, 2.00m } };
        var market = CreateTestMarket(outcomes: outcomes);

        market.Outcomes.Should().ContainKey(outcomeName);
        market.Outcomes[outcomeName].Should().Be(2.00m);
    }

    private static Market CreateTestMarket(
        Guid? eventId = null,
        string name = "Test Market",
        string description = "Test market description",
        Dictionary<string, decimal>? outcomes = null)
    {
        return Market.Create(
            eventId ?? Guid.NewGuid(),
            name,
            description,
            outcomes ?? new Dictionary<string, decimal> { { "home", 1.85m }, { "away", 2.10m } });
    }
}