using SportsbookLite.Contracts.Events;

namespace SportsbookLite.TestUtilities.TestDataBuilders;

public sealed class MarketTestDataBuilder
{
    private Guid _eventId = Guid.NewGuid();
    private string _name = "Default Market";
    private string _description = "Default market description";
    private Dictionary<string, decimal> _outcomes = new() { { "home", 1.85m }, { "away", 2.10m } };
    private MarketStatus _status = MarketStatus.Open;
    private string? _winningOutcome = null;
    private Guid? _customId = null;

    public static MarketTestDataBuilder Create() => new();

    public MarketTestDataBuilder WithEventId(Guid eventId)
    {
        _eventId = eventId;
        return this;
    }

    public MarketTestDataBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public MarketTestDataBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public MarketTestDataBuilder WithOutcomes(Dictionary<string, decimal> outcomes)
    {
        _outcomes = new Dictionary<string, decimal>(outcomes);
        return this;
    }

    public MarketTestDataBuilder WithOutcome(string outcome, decimal odds)
    {
        _outcomes[outcome] = odds;
        return this;
    }

    public MarketTestDataBuilder WithStatus(MarketStatus status)
    {
        _status = status;
        return this;
    }

    public MarketTestDataBuilder WithWinner(string winningOutcome)
    {
        _winningOutcome = winningOutcome;
        _status = MarketStatus.Settled;
        return this;
    }

    public MarketTestDataBuilder WithCustomId(Guid id)
    {
        _customId = id;
        return this;
    }

    public MarketTestDataBuilder Open()
    {
        _status = MarketStatus.Open;
        _winningOutcome = null;
        return this;
    }

    public MarketTestDataBuilder Suspended()
    {
        _status = MarketStatus.Suspended;
        return this;
    }

    public MarketTestDataBuilder Closed()
    {
        _status = MarketStatus.Closed;
        _winningOutcome = null;
        return this;
    }

    public MarketTestDataBuilder Settled(string? winner = null)
    {
        _status = MarketStatus.Settled;
        if (winner != null)
        {
            _winningOutcome = winner;
        }
        else if (_outcomes.Any())
        {
            _winningOutcome = _outcomes.Keys.First();
        }
        return this;
    }

    public Market Build()
    {
        var market = Market.Create(_eventId, _name, _description, _outcomes);

        if (_customId.HasValue)
        {
            market = market with { Id = _customId.Value };
        }

        if (_status != MarketStatus.Open)
        {
            market = market.WithStatus(_status);
        }

        if (!string.IsNullOrEmpty(_winningOutcome))
        {
            market = market.WithWinner(_winningOutcome);
        }

        return market;
    }

    public static class Scenarios
    {
        public static Market MatchWinner(Guid eventId, string homeTeam = "Home", string awayTeam = "Away")
        {
            return Create()
                .WithEventId(eventId)
                .WithName("Match Winner")
                .WithDescription("Select the winner of the match")
                .WithOutcomes(new Dictionary<string, decimal>
                {
                    { homeTeam.ToLower(), 1.85m },
                    { awayTeam.ToLower(), 2.10m },
                    { "draw", 3.40m }
                })
                .Build();
        }

        public static Market TotalGoals(Guid eventId, decimal overUnderLine = 2.5m)
        {
            return Create()
                .WithEventId(eventId)
                .WithName($"Total Goals O/U {overUnderLine}")
                .WithDescription($"Total goals over or under {overUnderLine}")
                .WithOutcomes(new Dictionary<string, decimal>
                {
                    { $"over_{overUnderLine}", 1.90m },
                    { $"under_{overUnderLine}", 1.90m }
                })
                .Build();
        }

        public static Market FirstGoalScorer(Guid eventId)
        {
            return Create()
                .WithEventId(eventId)
                .WithName("First Goal Scorer")
                .WithDescription("Who will score the first goal")
                .WithOutcomes(new Dictionary<string, decimal>
                {
                    { "player1", 3.50m },
                    { "player2", 4.20m },
                    { "player3", 5.00m },
                    { "no_goal", 15.00m }
                })
                .Build();
        }

        public static Market BothTeamsToScore(Guid eventId)
        {
            return Create()
                .WithEventId(eventId)
                .WithName("Both Teams To Score")
                .WithDescription("Will both teams score in the match")
                .WithOutcomes(new Dictionary<string, decimal>
                {
                    { "yes", 1.65m },
                    { "no", 2.20m }
                })
                .Build();
        }

        public static Market CorrectScore(Guid eventId)
        {
            return Create()
                .WithEventId(eventId)
                .WithName("Correct Score")
                .WithDescription("Predict the exact final score")
                .WithOutcomes(new Dictionary<string, decimal>
                {
                    { "1-0", 8.50m },
                    { "2-0", 12.00m },
                    { "2-1", 9.00m },
                    { "1-1", 6.50m },
                    { "0-0", 11.00m },
                    { "0-1", 15.00m },
                    { "0-2", 25.00m },
                    { "1-2", 18.00m }
                })
                .Build();
        }

        public static Market HalfTimeFullTime(Guid eventId)
        {
            return Create()
                .WithEventId(eventId)
                .WithName("Half Time / Full Time")
                .WithDescription("Result at half time and full time")
                .WithOutcomes(new Dictionary<string, decimal>
                {
                    { "home_home", 3.20m },
                    { "home_draw", 8.50m },
                    { "home_away", 25.00m },
                    { "draw_home", 4.50m },
                    { "draw_draw", 5.20m },
                    { "draw_away", 8.00m },
                    { "away_home", 35.00m },
                    { "away_draw", 12.00m },
                    { "away_away", 6.50m }
                })
                .Build();
        }

        public static Market SettledMatchWinner(Guid eventId, string winner = "home")
        {
            return Create()
                .WithEventId(eventId)
                .WithName("Match Winner")
                .WithDescription("Select the winner of the match")
                .WithOutcomes(new Dictionary<string, decimal>
                {
                    { "home", 1.85m },
                    { "away", 2.10m },
                    { "draw", 3.40m }
                })
                .Settled(winner)
                .Build();
        }

        public static Market SuspendedMarket(Guid eventId, string name = "Suspended Market")
        {
            return Create()
                .WithEventId(eventId)
                .WithName(name)
                .WithDescription("This market has been suspended")
                .Suspended()
                .Build();
        }

        public static Market ClosedMarket(Guid eventId, string name = "Closed Market")
        {
            return Create()
                .WithEventId(eventId)
                .WithName(name)
                .WithDescription("This market is closed for betting")
                .Closed()
                .Build();
        }
    }

    public static class Collections
    {
        public static IEnumerable<Market> StandardSoccerMarkets(Guid eventId)
        {
            yield return Scenarios.MatchWinner(eventId);
            yield return Scenarios.TotalGoals(eventId);
            yield return Scenarios.BothTeamsToScore(eventId);
            yield return Scenarios.FirstGoalScorer(eventId);
            yield return Scenarios.CorrectScore(eventId);
            yield return Scenarios.HalfTimeFullTime(eventId);
        }

        public static IEnumerable<Market> MarketsInDifferentStatuses(Guid eventId)
        {
            yield return Create().WithEventId(eventId).WithName("Open Market").Open().Build();
            yield return Create().WithEventId(eventId).WithName("Suspended Market").Suspended().Build();
            yield return Create().WithEventId(eventId).WithName("Closed Market").Closed().Build();
            yield return Create().WithEventId(eventId).WithName("Settled Market").Settled().Build();
        }

        public static IEnumerable<Market> MultipleMarkets(Guid eventId, int count = 5)
        {
            for (int i = 0; i < count; i++)
            {
                yield return Create()
                    .WithEventId(eventId)
                    .WithName($"Market {i + 1}")
                    .WithDescription($"Description for market {i + 1}")
                    .WithOutcomes(new Dictionary<string, decimal>
                    {
                        { $"outcome_a_{i}", 1.50m + (i * 0.1m) },
                        { $"outcome_b_{i}", 2.50m - (i * 0.1m) }
                    })
                    .Build();
            }
        }

        public static IEnumerable<Market> HighOddsMarkets(Guid eventId)
        {
            yield return Create()
                .WithEventId(eventId)
                .WithName("Long Shot Market")
                .WithOutcomes(new Dictionary<string, decimal>
                {
                    { "very_likely", 1.05m },
                    { "unlikely", 25.00m },
                    { "very_unlikely", 100.00m }
                })
                .Build();

            yield return Create()
                .WithEventId(eventId)
                .WithName("Even Money Market")
                .WithOutcomes(new Dictionary<string, decimal>
                {
                    { "option_a", 2.00m },
                    { "option_b", 2.00m }
                })
                .Build();
        }
    }
}