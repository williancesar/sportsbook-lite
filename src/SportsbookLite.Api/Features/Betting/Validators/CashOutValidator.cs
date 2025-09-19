using FluentValidation;
using SportsbookLite.Api.Features.Betting.Requests;

namespace SportsbookLite.Api.Features.Betting.Validators;

public sealed class CashOutValidator : Validator<CashOutRequest>
{
    public CashOutValidator()
    {
        RuleFor(x => x.BetId)
            .NotEqual(Guid.Empty)
            .WithMessage("Bet ID must be valid");

        RuleFor(x => x.AcceptablePayout)
            .GreaterThan(0)
            .When(x => x.AcceptablePayout.HasValue)
            .WithMessage("Acceptable payout must be greater than 0 when specified");
    }
}