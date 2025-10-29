using FastEndpoints;
using FluentValidation;
using SportsbookLite.Api.Features.Wallet.Requests;

namespace SportsbookLite.Api.Features.Wallet.Validators;

public sealed class WithdrawValidator : Validator<WithdrawRequest>
{
    public WithdrawValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required")
            .Length(1, 100)
            .WithMessage("User ID must be between 1 and 100 characters");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0")
            .LessThanOrEqualTo(1000000)
            .WithMessage("Amount cannot exceed 1,000,000")
            .Must(BeValidDecimal)
            .WithMessage("Amount cannot have more than 2 decimal places");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required")
            .Length(3)
            .WithMessage("Currency must be exactly 3 characters")
            .Must(BeValidCurrency)
            .WithMessage("Currency must be a valid 3-letter code");

        RuleFor(x => x.TransactionId)
            .NotEmpty()
            .WithMessage("Transaction ID is required")
            .Length(1, 100)
            .WithMessage("Transaction ID must be between 1 and 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage("Description cannot exceed 500 characters");
    }

    private static bool BeValidDecimal(decimal amount)
    {
        var decimalPlaces = BitConverter.GetBytes(decimal.GetBits(amount)[3])[2];
        return decimalPlaces <= 2;
    }

    private static bool BeValidCurrency(string currency)
    {
        var validCurrencies = new[] { "USD", "EUR", "GBP", "CAD", "AUD" };
        return validCurrencies.Contains(currency.ToUpperInvariant());
    }
}