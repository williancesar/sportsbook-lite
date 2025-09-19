using FastEndpoints;
using FluentValidation;
using SportsbookLite.Api.Features.Events.Requests;

namespace SportsbookLite.Api.Features.Events.Validators;

public sealed class UpdateEventValidator : Validator<UpdateEventRequest>
{
    public UpdateEventValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty()
            .WithMessage("Event ID is required");

        RuleFor(x => x.Name)
            .Length(1, 200)
            .WithMessage("Event name must be between 1 and 200 characters")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.StartTime)
            .GreaterThan(DateTimeOffset.UtcNow)
            .WithMessage("Start time must be in the future")
            .When(x => x.StartTime.HasValue);

        RuleFor(x => x.Participants)
            .Must(participants => participants!.Count >= 2)
            .WithMessage("At least two participants are required")
            .When(x => x.Participants != null);

        RuleForEach(x => x.Participants!)
            .Must(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
            .WithMessage("Participant key and value cannot be empty")
            .When(x => x.Participants != null);

        RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x.Name) || x.StartTime.HasValue || x.Participants != null)
            .WithMessage("At least one field must be provided for update");
    }
}