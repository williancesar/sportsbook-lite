using FastEndpoints;
using FluentValidation;
using SportsbookLite.Api.Features.Events.Requests;

namespace SportsbookLite.Api.Features.Events.Validators;

public sealed class AddMarketValidator : Validator<AddMarketRequest>
{
    public AddMarketValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty()
            .WithMessage("Event ID is required");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Market name is required")
            .Length(1, 100)
            .WithMessage("Market name must be between 1 and 100 characters");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Market description is required")
            .Length(1, 500)
            .WithMessage("Market description must be between 1 and 500 characters");

        RuleFor(x => x.Outcomes)
            .NotEmpty()
            .WithMessage("At least one outcome is required")
            .Must(outcomes => outcomes.Count >= 2)
            .WithMessage("At least two outcomes are required");

        RuleForEach(x => x.Outcomes)
            .Must(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
            .WithMessage("Outcome name cannot be empty")
            .Must(kvp => kvp.Value > 0)
            .WithMessage("Outcome odds must be greater than 0");
    }
}