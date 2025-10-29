using FastEndpoints;
using FluentValidation;
using SportsbookLite.Api.Features.Odds.Requests;

namespace SportsbookLite.Api.Features.Odds.Validators;

public sealed class LockOddsValidator : Validator<LockOddsRequest>
{
    public LockOddsValidator()
    {
        RuleFor(x => x.BetId)
            .NotEmpty()
            .WithMessage("BetId is required")
            .Length(1, 100)
            .WithMessage("BetId must be between 1 and 100 characters");

        RuleFor(x => x.SelectionId)
            .NotEmpty()
            .WithMessage("SelectionId is required")
            .Length(1, 100)
            .WithMessage("SelectionId must be between 1 and 100 characters");
    }
}