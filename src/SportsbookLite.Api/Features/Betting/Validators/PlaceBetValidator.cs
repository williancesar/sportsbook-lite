using FluentValidation;
using SportsbookLite.Api.Features.Betting.Requests;

namespace SportsbookLite.Api.Features.Betting.Validators;

public sealed class PlaceBetValidator : Validator<PlaceBetApiRequest>
{
    public PlaceBetValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.EventId)
            .NotEqual(Guid.Empty)
            .WithMessage("Event ID must be valid");

        RuleFor(x => x.MarketId)
            .NotEmpty()
            .WithMessage("Market ID is required");

        RuleFor(x => x.SelectionId)
            .NotEmpty()
            .WithMessage("Selection ID is required");

        RuleFor(x => x.Stake)
            .GreaterThan(0)
            .WithMessage("Stake must be greater than 0")
            .LessThanOrEqualTo(10000)
            .WithMessage("Stake cannot exceed 10,000");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .WithMessage("Currency must be a valid 3-character code");

        RuleFor(x => x.AcceptableOdds)
            .GreaterThan(1)
            .WithMessage("Acceptable odds must be greater than 1.00")
            .LessThanOrEqualTo(1000)
            .WithMessage("Acceptable odds cannot exceed 1,000");
    }
}