using SportsbookLite.Contracts.Events;

namespace SportsbookLite.TestUtilities.TestDataBuilders;

public sealed class EventTestDataBuilder
{
    private string _name = "Default Test Event";
    private SportType _sportType = SportType.Soccer;
    private string _competition = "Default League";
    private DateTimeOffset _startTime = DateTimeOffset.UtcNow.AddDays(1);
    private Dictionary<string, string> _participants = new() { { "home", "Team A" }, { "away", "Team B" } };
    private EventStatus _status = EventStatus.Scheduled;
    private DateTimeOffset? _endTime = null;
    private Guid? _customId = null;

    public static EventTestDataBuilder Create() => new();

    public EventTestDataBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public EventTestDataBuilder WithSportType(SportType sportType)
    {
        _sportType = sportType;
        return this;
    }

    public EventTestDataBuilder WithCompetition(string competition)
    {
        _competition = competition;
        return this;
    }

    public EventTestDataBuilder WithStartTime(DateTimeOffset startTime)
    {
        _startTime = startTime;
        return this;
    }

    public EventTestDataBuilder WithParticipants(Dictionary<string, string> participants)
    {
        _participants = new Dictionary<string, string>(participants);
        return this;
    }

    public EventTestDataBuilder WithParticipant(string role, string name)
    {
        _participants[role] = name;
        return this;
    }

    public EventTestDataBuilder WithHomeTeam(string teamName)
    {
        _participants["home"] = teamName;
        return this;
    }

    public EventTestDataBuilder WithAwayTeam(string teamName)
    {
        _participants["away"] = teamName;
        return this;
    }

    public EventTestDataBuilder WithStatus(EventStatus status)
    {
        _status = status;
        return this;
    }

    public EventTestDataBuilder WithEndTime(DateTimeOffset endTime)
    {
        _endTime = endTime;
        return this;
    }

    public EventTestDataBuilder WithCustomId(Guid id)
    {
        _customId = id;
        return this;
    }

    public EventTestDataBuilder Scheduled()
    {
        _status = EventStatus.Scheduled;
        _endTime = null;
        return this;
    }

    public EventTestDataBuilder Live()
    {
        _status = EventStatus.Live;
        _endTime = null;
        return this;
    }

    public EventTestDataBuilder Completed()
    {
        _status = EventStatus.Completed;
        _endTime ??= DateTimeOffset.UtcNow;
        return this;
    }

    public EventTestDataBuilder Cancelled()
    {
        _status = EventStatus.Cancelled;
        return this;
    }

    public EventTestDataBuilder Suspended()
    {
        _status = EventStatus.Suspended;
        return this;
    }

    public EventTestDataBuilder StartingInDays(int days)
    {
        _startTime = DateTimeOffset.UtcNow.AddDays(days);
        return this;
    }

    public EventTestDataBuilder StartingInHours(int hours)
    {
        _startTime = DateTimeOffset.UtcNow.AddHours(hours);
        return this;
    }

    public EventTestDataBuilder StartingInMinutes(int minutes)
    {
        _startTime = DateTimeOffset.UtcNow.AddMinutes(minutes);
        return this;
    }

    public EventTestDataBuilder Soccer() => WithSportType(SportType.Soccer);
    public EventTestDataBuilder Basketball() => WithSportType(SportType.Basketball);
    public EventTestDataBuilder Tennis() => WithSportType(SportType.Tennis);
    public EventTestDataBuilder Football() => WithSportType(SportType.Football);
    public EventTestDataBuilder Baseball() => WithSportType(SportType.Baseball);
    public EventTestDataBuilder Hockey() => WithSportType(SportType.Hockey);

    public SportEvent Build()
    {
        var baseEvent = SportEvent.Create(_name, _sportType, _competition, _startTime, _participants);

        if (_customId.HasValue)
        {
            baseEvent = baseEvent with { Id = _customId.Value };
        }

        if (_status != EventStatus.Scheduled || _endTime.HasValue)
        {
            baseEvent = baseEvent.WithStatus(_status, _endTime);
        }

        return baseEvent;
    }

    public static class Scenarios
    {
        public static SportEvent PremierLeagueMatch(string homeTeam = "Liverpool", string awayTeam = "Arsenal")
        {
            return Create()
                .WithName($"{homeTeam} vs {awayTeam}")
                .Soccer()
                .WithCompetition("Premier League")
                .WithHomeTeam(homeTeam)
                .WithAwayTeam(awayTeam)
                .StartingInDays(1)
                .Build();
        }

        public static SportEvent NBAGame(string homeTeam = "Lakers", string awayTeam = "Warriors")
        {
            return Create()
                .WithName($"{homeTeam} vs {awayTeam}")
                .Basketball()
                .WithCompetition("NBA")
                .WithHomeTeam(homeTeam)
                .WithAwayTeam(awayTeam)
                .StartingInHours(8)
                .Build();
        }

        public static SportEvent TennisMatch(string player1 = "Djokovic", string player2 = "Nadal")
        {
            return Create()
                .WithName($"{player1} vs {player2}")
                .Tennis()
                .WithCompetition("Wimbledon")
                .WithParticipants(new Dictionary<string, string>
                {
                    { "player1", player1 },
                    { "player2", player2 }
                })
                .StartingInHours(24)
                .Build();
        }

        public static SportEvent NFLGame(string homeTeam = "Patriots", string awayTeam = "Chiefs")
        {
            return Create()
                .WithName($"{homeTeam} vs {awayTeam}")
                .Football()
                .WithCompetition("NFL")
                .WithHomeTeam(homeTeam)
                .WithAwayTeam(awayTeam)
                .StartingInDays(3)
                .Build();
        }

        public static SportEvent LiveEvent(string name = "Live Test Event")
        {
            return Create()
                .WithName(name)
                .Live()
                .StartingInHours(-1)
                .Build();
        }

        public static SportEvent CompletedEvent(string name = "Completed Test Event")
        {
            return Create()
                .WithName(name)
                .Completed()
                .StartingInHours(-3)
                .WithEndTime(DateTimeOffset.UtcNow.AddHours(-1))
                .Build();
        }

        public static SportEvent CancelledEvent(string name = "Cancelled Test Event")
        {
            return Create()
                .WithName(name)
                .Cancelled()
                .StartingInDays(1)
                .Build();
        }
    }

    public static class Collections
    {
        public static IEnumerable<SportEvent> MultipleEvents(int count = 5)
        {
            for (int i = 0; i < count; i++)
            {
                yield return Create()
                    .WithName($"Event {i + 1}")
                    .StartingInDays(i + 1)
                    .Build();
            }
        }

        public static IEnumerable<SportEvent> MixedSportEvents()
        {
            yield return Scenarios.PremierLeagueMatch();
            yield return Scenarios.NBAGame();
            yield return Scenarios.TennisMatch();
            yield return Scenarios.NFLGame();
        }

        public static IEnumerable<SportEvent> EventsInDifferentStatuses()
        {
            yield return Create().Scheduled().WithName("Scheduled Event").Build();
            yield return Create().Live().WithName("Live Event").Build();
            yield return Create().Completed().WithName("Completed Event").Build();
            yield return Create().Cancelled().WithName("Cancelled Event").Build();
            yield return Create().Suspended().WithName("Suspended Event").Build();
        }
    }
}