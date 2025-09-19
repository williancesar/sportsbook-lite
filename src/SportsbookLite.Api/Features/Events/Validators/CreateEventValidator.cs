using FastEndpoints;
using FluentValidation;
using SportsbookLite.Api.Features.Events.Requests;
using SportsbookLite.Contracts.Events;

namespace SportsbookLite.Api.Features.Events.Validators;

public sealed class CreateEventValidator : Validator<CreateEventRequest>
{
    public CreateEventValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Event name is required")
            .Length(1, 200)
            .WithMessage("Event name must be between 1 and 200 characters");

        RuleFor(x => x.SportType)
            .IsInEnum()
            .WithMessage("SportType must be a valid sport type");

        RuleFor(x => x.Competition)
            .NotEmpty()
            .WithMessage("Competition is required")
            .Length(1, 100)
            .WithMessage("Competition must be between 1 and 100 characters");

        RuleFor(x => x.StartTime)
            .GreaterThan(DateTimeOffset.UtcNow)
            .WithMessage("Start time must be in the future");

        RuleFor(x => x.Participants)
            .NotEmpty()
            .WithMessage("At least one participant is required")
            .Must(participants => participants.Count >= 2)
            .WithMessage("At least two participants are required");

        RuleForEach(x => x.Participants)
            .Must(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
            .WithMessage("Participant key and value cannot be empty");
    }
}