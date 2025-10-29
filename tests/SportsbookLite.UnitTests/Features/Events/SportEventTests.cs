using FluentAssertions;
using SportsbookLite.Contracts.Events;
using SportsbookLite.TestUtilities;

namespace SportsbookLite.UnitTests.Features.Events;

public class SportEventTests : BaseUnitTest
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        var name = "Liverpool vs Arsenal";
        var sportType = SportType.Soccer;
        var competition = "Premier League";
        var startTime = DateTimeOffset.UtcNow.AddDays(1);
        var participants = new Dictionary<string, string>
        {
            { "home", "Liverpool" },
            { "away", "Arsenal" }
        };

        var sportEvent = SportEvent.Create(name, sportType, competition, startTime, participants);

        sportEvent.Id.Should().NotBeEmpty();
        sportEvent.Name.Should().Be(name);
        sportEvent.SportType.Should().Be(sportType);
        sportEvent.Competition.Should().Be(competition);
        sportEvent.StartTime.Should().Be(startTime);
        sportEvent.Status.Should().Be(EventStatus.Scheduled);
        sportEvent.Participants.Should().BeEquivalentTo(participants);
        sportEvent.EndTime.Should().BeNull();
        sportEvent.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        sportEvent.LastModified.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void WithStatus_FromScheduledToLive_ShouldSucceed()
    {
        var sportEvent = CreateTestEvent();

        var updatedEvent = sportEvent.WithStatus(EventStatus.Live);

        updatedEvent.Status.Should().Be(EventStatus.Live);
        updatedEvent.Id.Should().Be(sportEvent.Id);
        updatedEvent.Name.Should().Be(sportEvent.Name);
        updatedEvent.LastModified.Should().BeAfter(sportEvent.LastModified);
        updatedEvent.EndTime.Should().BeNull();
    }

    [Fact]
    public void WithStatus_FromLiveToCompleted_ShouldSucceed()
    {
        var sportEvent = CreateTestEvent().WithStatus(EventStatus.Live);
        var endTime = DateTimeOffset.UtcNow;

        var updatedEvent = sportEvent.WithStatus(EventStatus.Completed, endTime);

        updatedEvent.Status.Should().Be(EventStatus.Completed);
        updatedEvent.EndTime.Should().Be(endTime);
        updatedEvent.LastModified.Should().BeAfter(sportEvent.LastModified);
    }

    [Fact]
    public void WithUpdatedTime_ShouldUpdateStartTimeAndModified()
    {
        var sportEvent = CreateTestEvent();
        var newStartTime = DateTimeOffset.UtcNow.AddDays(2);

        var updatedEvent = sportEvent.WithUpdatedTime(newStartTime);

        updatedEvent.StartTime.Should().Be(newStartTime);
        updatedEvent.LastModified.Should().BeAfter(sportEvent.LastModified);
        updatedEvent.Id.Should().Be(sportEvent.Id);
        updatedEvent.Status.Should().Be(sportEvent.Status);
    }

    [Theory]
    [InlineData(EventStatus.Scheduled, EventStatus.Live, true)]
    [InlineData(EventStatus.Scheduled, EventStatus.Suspended, true)]
    [InlineData(EventStatus.Scheduled, EventStatus.Cancelled, true)]
    [InlineData(EventStatus.Live, EventStatus.Completed, true)]
    [InlineData(EventStatus.Live, EventStatus.Suspended, true)]
    [InlineData(EventStatus.Suspended, EventStatus.Scheduled, true)]
    [InlineData(EventStatus.Suspended, EventStatus.Live, true)]
    [InlineData(EventStatus.Suspended, EventStatus.Cancelled, true)]
    public void CanTransitionTo_ValidTransitions_ShouldReturnTrue(EventStatus fromStatus, EventStatus toStatus, bool expected)
    {
        var sportEvent = CreateTestEvent().WithStatus(fromStatus);

        var result = sportEvent.CanTransitionTo(toStatus);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(EventStatus.Scheduled, EventStatus.Completed)]
    [InlineData(EventStatus.Completed, EventStatus.Live)]
    [InlineData(EventStatus.Completed, EventStatus.Scheduled)]
    [InlineData(EventStatus.Completed, EventStatus.Suspended)]
    [InlineData(EventStatus.Cancelled, EventStatus.Live)]
    [InlineData(EventStatus.Cancelled, EventStatus.Scheduled)]
    [InlineData(EventStatus.Cancelled, EventStatus.Completed)]
    [InlineData(EventStatus.Live, EventStatus.Scheduled)]
    [InlineData(EventStatus.Live, EventStatus.Cancelled)]
    public void CanTransitionTo_InvalidTransitions_ShouldReturnFalse(EventStatus fromStatus, EventStatus toStatus)
    {
        var sportEvent = CreateTestEvent().WithStatus(fromStatus);

        var result = sportEvent.CanTransitionTo(toStatus);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanTransitionTo_SameStatus_ShouldReturnFalse()
    {
        var sportEvent = CreateTestEvent();

        var result = sportEvent.CanTransitionTo(EventStatus.Scheduled);

        result.Should().BeFalse();
    }

    [Fact]
    public void WithStatus_WithEndTime_ShouldSetEndTime()
    {
        var sportEvent = CreateTestEvent().WithStatus(EventStatus.Live);
        var endTime = DateTimeOffset.UtcNow.AddHours(2);

        var updatedEvent = sportEvent.WithStatus(EventStatus.Completed, endTime);

        updatedEvent.EndTime.Should().Be(endTime);
        updatedEvent.Status.Should().Be(EventStatus.Completed);
    }

    [Fact]
    public void WithStatus_WithoutEndTime_ShouldKeepExistingEndTime()
    {
        var existingEndTime = DateTimeOffset.UtcNow.AddHours(1);
        var sportEvent = CreateTestEvent().WithStatus(EventStatus.Completed, existingEndTime);

        var updatedEvent = sportEvent.WithStatus(EventStatus.Suspended);

        updatedEvent.EndTime.Should().Be(existingEndTime);
        updatedEvent.Status.Should().Be(EventStatus.Suspended);
    }

    [Fact]
    public void Create_DefaultsToScheduledStatus_Always()
    {
        var sportEvent = CreateTestEvent();

        sportEvent.Status.Should().Be(EventStatus.Scheduled);
    }

    [Fact]
    public void Equality_WithSameValues_ShouldBeEqual()
    {
        var participants = new Dictionary<string, string> { { "home", "Team A" }, { "away", "Team B" } };
        var startTime = DateTimeOffset.UtcNow.AddDays(1);
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var event1 = new SportEvent(id, "Test Event", SportType.Soccer, "Test League", startTime, null, EventStatus.Scheduled, participants, createdAt, createdAt);
        var event2 = new SportEvent(id, "Test Event", SportType.Soccer, "Test League", startTime, null, EventStatus.Scheduled, participants, createdAt, createdAt);

        event1.Should().Be(event2);
        (event1 == event2).Should().BeTrue();
        (event1 != event2).Should().BeFalse();
    }

    [Fact]
    public void Equality_WithDifferentIds_ShouldNotBeEqual()
    {
        var participants = new Dictionary<string, string> { { "home", "Team A" }, { "away", "Team B" } };
        var startTime = DateTimeOffset.UtcNow.AddDays(1);
        var createdAt = DateTimeOffset.UtcNow;

        var event1 = new SportEvent(Guid.NewGuid(), "Test Event", SportType.Soccer, "Test League", startTime, null, EventStatus.Scheduled, participants, createdAt, createdAt);
        var event2 = new SportEvent(Guid.NewGuid(), "Test Event", SportType.Soccer, "Test League", startTime, null, EventStatus.Scheduled, participants, createdAt, createdAt);

        event1.Should().NotBe(event2);
        (event1 == event2).Should().BeFalse();
        (event1 != event2).Should().BeTrue();
    }

    [Fact]
    public void Participants_Modification_ShouldNotAffectRecord()
    {
        var participants = new Dictionary<string, string> { { "home", "Team A" }, { "away", "Team B" } };
        var sportEvent = CreateTestEvent(participants: participants);
        var originalParticipantsCount = sportEvent.Participants.Count;

        var modifiedParticipants = new Dictionary<string, string>(sportEvent.Participants)
        {
            ["neutral"] = "Team C"
        };

        sportEvent.Participants.Should().HaveCount(originalParticipantsCount);
        sportEvent.Participants.Should().NotContainKey("neutral");
        modifiedParticipants.Should().ContainKey("neutral");
    }

    private static SportEvent CreateTestEvent(
        string name = "Test Event",
        SportType sportType = SportType.Soccer,
        string competition = "Test League",
        DateTimeOffset? startTime = null,
        Dictionary<string, string>? participants = null)
    {
        return SportEvent.Create(
            name,
            sportType,
            competition,
            startTime ?? DateTimeOffset.UtcNow.AddDays(1),
            participants ?? new Dictionary<string, string> { { "home", "Team A" }, { "away", "Team B" } });
    }
}