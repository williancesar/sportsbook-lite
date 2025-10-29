using Bogus;
using SportsbookLite.Contracts.Odds;
using OddsValue = SportsbookLite.Contracts.Odds.Odds;

namespace SportsbookLite.TestUtilities.TestDataBuilders;

public class OddsTestDataBuilder
{
    private static readonly Faker _faker = new Faker();

    public static OddsValue CreateValidOdds(
        decimal? decimalOdds = null,
        string? marketId = null,
        string? selection = null,
        OddsSource? source = null)
    {
        return OddsValue.Create(
            decimalOdds ?? _faker.Random.Decimal(1.01m, 10.0m),
            marketId ?? _faker.Random.AlphaNumeric(10),
            selection ?? _faker.PickRandom("Home Win", "Draw", "Away Win", "Over 2.5", "Under 2.5"),
            source ?? _faker.PickRandom<OddsSource>());
    }

    public static OddsValue CreateOddsWithDecimal(
        decimal decimalOdds,
        string? marketId = null,
        string? selection = null)
    {
        return OddsValue.Create(
            decimalOdds,
            marketId ?? $"MATCH_{_faker.Random.Number(1000, 9999)}",
            selection ?? _faker.PickRandom("Home Win", "Draw", "Away Win"));
    }

    public static OddsValue CreateFavouriteOdds(
        string? marketId = null,
        string? selection = null)
    {
        return OddsValue.Create(
            _faker.Random.Decimal(1.01m, 2.0m),
            marketId ?? $"MATCH_{_faker.Random.Number(1000, 9999)}",
            selection ?? "Favourite");
    }

    public static OddsValue CreateOutsiderOdds(
        string? marketId = null,
        string? selection = null)
    {
        return OddsValue.Create(
            _faker.Random.Decimal(5.0m, 20.0m),
            marketId ?? $"MATCH_{_faker.Random.Number(1000, 9999)}",
            selection ?? "Outsider");
    }

    public static OddsUpdateRequest CreateValidUpdateRequest(
        string? marketId = null,
        Dictionary<string, decimal>? selectionOdds = null,
        OddsSource? source = null,
        string? reason = null,
        string? updatedBy = null)
    {
        return OddsUpdateRequest.Create(
            marketId ?? $"MATCH_{_faker.Random.Number(1000, 9999)}",
            selectionOdds ?? new Dictionary<string, decimal>
            {
                ["Home Win"] = _faker.Random.Decimal(1.5m, 3.0m),
                ["Draw"] = _faker.Random.Decimal(2.8m, 4.5m),
                ["Away Win"] = _faker.Random.Decimal(2.0m, 8.0m)
            },
            source ?? _faker.PickRandom<OddsSource>(),
            reason ?? _faker.Lorem.Sentence(3, 5),
            updatedBy ?? _faker.Name.FullName());
    }

    public static OddsUpdateRequest CreateHighVolatilityUpdate(
        string marketId,
        string selection,
        decimal baseOdds)
    {
        var volatileChange = _faker.Random.Bool() ? 0.8m : -0.6m;
        var newOdds = Math.Max(1.01m, baseOdds + volatileChange);

        return OddsUpdateRequest.Create(
            marketId,
            new Dictionary<string, decimal> { [selection] = newOdds },
            OddsSource.Feed,
            "High volatility test update");
    }

    public static OddsUpdate CreateValidOddsUpdate(
        OddsValue? previousOdds = null,
        OddsValue? newOdds = null,
        OddsSource? source = null,
        string? reason = null)
    {
        var marketId = $"MATCH_{_faker.Random.Number(1000, 9999)}";
        var selection = _faker.PickRandom("Home Win", "Draw", "Away Win");

        var prevOdds = previousOdds ?? OddsValue.Create(2.0m, marketId, selection);
        var nextOdds = newOdds ?? OddsValue.Create(2.2m, marketId, selection);

        return OddsUpdate.Create(
            prevOdds,
            nextOdds,
            source ?? _faker.PickRandom<OddsSource>(),
            reason ?? _faker.Lorem.Sentence(3, 5));
    }

    public static OddsHistory CreateOddsHistory(
        string? marketId = null,
        string? selection = null,
        int updateCount = 5)
    {
        var mId = marketId ?? $"MATCH_{_faker.Random.Number(1000, 9999)}";
        var sel = selection ?? _faker.PickRandom("Home Win", "Draw", "Away Win");
        
        var initialOdds = OddsValue.Create(2.0m, mId, sel);
        var history = OddsHistory.Create(mId, sel, initialOdds);

        var currentOdds = 2.0m;
        for (int i = 0; i < updateCount; i++)
        {
            var previousOddsValue = OddsValue.Create(currentOdds, mId, sel);
            currentOdds += _faker.Random.Decimal(-0.3m, 0.3m);
            currentOdds = Math.Max(1.01m, currentOdds);
            
            var newOddsValue = OddsValue.Create(currentOdds, mId, sel);
            var update = OddsUpdate.Create(
                previousOddsValue,
                newOddsValue,
                _faker.PickRandom<OddsSource>(),
                $"Test update {i + 1}");

            history = history.AddUpdate(update);
        }

        return history;
    }

    public static OddsSnapshot CreateOddsSnapshot(
        string? marketId = null,
        Dictionary<string, OddsValue>? selections = null,
        OddsVolatility? volatility = null,
        bool? isSuspended = null,
        string? suspensionReason = null)
    {
        var mId = marketId ?? $"MATCH_{_faker.Random.Number(1000, 9999)}";
        var sels = selections ?? new Dictionary<string, OddsValue>
        {
            ["Home Win"] = OddsValue.Create(2.0m, mId, "Home Win"),
            ["Draw"] = OddsValue.Create(3.2m, mId, "Draw"),
            ["Away Win"] = OddsValue.Create(4.5m, mId, "Away Win")
        };

        var snapshot = OddsSnapshot.Create(mId, sels, volatility ?? OddsVolatility.Low);

        if (isSuspended == true)
        {
            snapshot = snapshot.WithSuspension(suspensionReason ?? "Test suspension");
        }

        return snapshot;
    }

    public static Dictionary<string, decimal> CreateValidSelectionOdds(
        params string[] selections)
    {
        var selectionOdds = new Dictionary<string, decimal>();
        
        if (!selections.Any())
        {
            selections = new[] { "Home Win", "Draw", "Away Win" };
        }

        foreach (var selection in selections)
        {
            selectionOdds[selection] = _faker.Random.Decimal(1.2m, 8.0m);
        }

        return selectionOdds;
    }

    public static Dictionary<string, OddsValue> CreateValidOddsSelections(
        string marketId,
        params string[] selections)
    {
        var oddsSelections = new Dictionary<string, OddsValue>();
        
        if (!selections.Any())
        {
            selections = new[] { "Home Win", "Draw", "Away Win" };
        }

        foreach (var selection in selections)
        {
            oddsSelections[selection] = OddsValue.Create(
                _faker.Random.Decimal(1.2m, 8.0m),
                marketId,
                selection);
        }

        return oddsSelections;
    }

    public static List<OddsUpdate> CreateUpdateSequence(
        string marketId,
        string selection,
        int count = 10,
        decimal startingOdds = 2.0m,
        decimal maxChange = 0.5m)
    {
        var updates = new List<OddsUpdate>();
        var currentOdds = startingOdds;

        for (int i = 0; i < count; i++)
        {
            var previousOddsValue = OddsValue.Create(currentOdds, marketId, selection);
            var change = _faker.Random.Decimal(-maxChange, maxChange);
            currentOdds = Math.Max(1.01m, currentOdds + change);
            var newOddsValue = OddsValue.Create(currentOdds, marketId, selection);

            var update = OddsUpdate.Create(
                previousOddsValue,
                newOddsValue,
                _faker.PickRandom<OddsSource>(),
                $"Sequence update {i + 1}");

            updates.Add(update);
        }

        return updates;
    }

    public static List<OddsValue> CreateOddsProgression(
        string marketId,
        string selection,
        decimal[] oddsValues)
    {
        return oddsValues.Select(odds => OddsValue.Create(odds, marketId, selection)).ToList();
    }

    public static class CommonOdds
    {
        public static OddsValue EvensDecimal => OddsValue.Create(2.0m, "TEST_MATCH", "Evens");
        public static OddsValue ShortFavourite => OddsValue.Create(1.5m, "TEST_MATCH", "Short Favourite");
        public static OddsValue LongShot => OddsValue.Create(10.0m, "TEST_MATCH", "Long Shot");
        public static OddsValue SlightFavourite => OddsValue.Create(1.8m, "TEST_MATCH", "Slight Favourite");
        public static OddsValue ModerateOutsider => OddsValue.Create(4.0m, "TEST_MATCH", "Moderate Outsider");
    }

    public static class CommonMarkets
    {
        public static Dictionary<string, decimal> MatchWinner => new()
        {
            ["Home Win"] = 2.1m,
            ["Draw"] = 3.4m,
            ["Away Win"] = 3.8m
        };

        public static Dictionary<string, decimal> OverUnder => new()
        {
            ["Over 2.5"] = 1.9m,
            ["Under 2.5"] = 1.95m
        };

        public static Dictionary<string, decimal> BothTeamsToScore => new()
        {
            ["Yes"] = 1.7m,
            ["No"] = 2.2m
        };

        public static Dictionary<string, decimal> AsianHandicap => new()
        {
            ["Home -1"] = 2.0m,
            ["Away +1"] = 1.85m
        };
    }

    public static class TestScenarios
    {
        public static OddsUpdateRequest CreateSuspensionTriggeringUpdate(string marketId)
        {
            return OddsUpdateRequest.Create(
                marketId,
                new Dictionary<string, decimal>
                {
                    ["Home Win"] = 1.1m,
                    ["Draw"] = 15.0m,
                    ["Away Win"] = 25.0m
                },
                OddsSource.Feed,
                "Extreme odds change - should trigger suspension");
        }

        public static List<OddsUpdateRequest> CreateVolatilitySequence(string marketId, int count = 20)
        {
            var updates = new List<OddsUpdateRequest>();
            var baseOdds = 2.0m;

            for (int i = 0; i < count; i++)
            {
                var volatileChange = i % 2 == 0 ? 0.8m : -0.6m;
                baseOdds = Math.Max(1.01m, baseOdds + volatileChange);

                updates.Add(OddsUpdateRequest.Create(
                    marketId,
                    new Dictionary<string, decimal> { ["Home Win"] = baseOdds },
                    OddsSource.Feed,
                    $"Volatility sequence update {i + 1}"));
            }

            return updates;
        }

        public static OddsHistory CreateHighVolatilityHistory(string marketId, string selection)
        {
            var initialOdds = OddsValue.Create(2.0m, marketId, selection);
            var history = OddsHistory.Create(marketId, selection, initialOdds);

            var volatileUpdates = new[]
            {
                (previous: 2.0m, next: 3.5m),
                (previous: 3.5m, next: 1.6m),
                (previous: 1.6m, next: 4.2m),
                (previous: 4.2m, next: 1.8m),
                (previous: 1.8m, next: 5.0m)
            };

            foreach (var (previous, next) in volatileUpdates)
            {
                var update = OddsUpdate.Create(
                    OddsValue.Create(previous, marketId, selection),
                    OddsValue.Create(next, marketId, selection),
                    OddsSource.Feed,
                    "High volatility test update");

                history = history.AddUpdate(update);
            }

            return history;
        }
    }

    public static OddsValue CreateRandomOdds(string? marketId = null, string? selection = null)
    {
        return OddsValue.Create(
            _faker.Random.Decimal(1.01m, 50.0m),
            marketId ?? $"RANDOM_{_faker.Random.AlphaNumeric(8)}",
            selection ?? _faker.PickRandom("Option A", "Option B", "Option C", "Draw", "Over", "Under"),
            _faker.PickRandom<OddsSource>());
    }

    public static List<OddsValue> CreateRandomOddsList(
        int count,
        string? marketId = null,
        decimal minOdds = 1.01m,
        decimal maxOdds = 20.0m)
    {
        var odds = new List<OddsValue>();
        var mId = marketId ?? $"RANDOM_{_faker.Random.AlphaNumeric(8)}";

        for (int i = 0; i < count; i++)
        {
            odds.Add(OddsValue.Create(
                _faker.Random.Decimal(minOdds, maxOdds),
                mId,
                $"Selection_{i + 1}",
                _faker.PickRandom<OddsSource>()));
        }

        return odds;
    }

    public static OddsUpdateRequest CreateEmptyUpdateRequest()
    {
        return new OddsUpdateRequest(
            MarketId: "",
            SelectionOdds: new Dictionary<string, decimal>(),
            Source: OddsSource.Manual,
            Reason: null,
            UpdatedBy: null,
            RequestedAt: DateTimeOffset.UtcNow);
    }

    public static OddsUpdateRequest CreateInvalidUpdateRequest()
    {
        return new OddsUpdateRequest(
            MarketId: "INVALID_MARKET",
            SelectionOdds: new Dictionary<string, decimal>
            {
                [""] = 0m,
                ["Invalid"] = -1.5m
            },
            Source: OddsSource.Manual,
            Reason: null,
            UpdatedBy: null,
            RequestedAt: DateTimeOffset.UtcNow);
    }
}