using FastEndpoints;
using FluentValidation;
using SportsbookLite.Api.Features.Odds.Requests;

namespace SportsbookLite.Api.Features.Odds.Validators;

public sealed class UpdateOddsValidator : Validator<UpdateOddsRequest>
{
    public UpdateOddsValidator()
    {
        RuleFor(x => x.Selections)
            .NotEmpty()
            .WithMessage("At least one selection with odds is required")
            .Must(selections => selections.All(kvp => !string.IsNullOrWhiteSpace(kvp.Key)))
            .WithMessage("Selection names cannot be empty");

        RuleFor(x => x.Selections)
            .Must(selections => selections.All(kvp => kvp.Value > 1.01m))
            .WithMessage("All odds values must be greater than 1.01")
            .When(x => x.Selections.Any());

        RuleFor(x => x.Selections)
            .Must(selections => selections.All(kvp => kvp.Value <= 1000m))
            .WithMessage("All odds values must be less than or equal to 1000")
            .When(x => x.Selections.Any());

        RuleFor(x => x.UpdatedBy)
            .Length(1, 100)
            .WithMessage("UpdatedBy must be between 1 and 100 characters")
            .When(x => !string.IsNullOrEmpty(x.UpdatedBy));

        RuleFor(x => x.Reason)
            .Length(1, 500)
            .WithMessage("Reason must be between 1 and 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Reason));
    }
}