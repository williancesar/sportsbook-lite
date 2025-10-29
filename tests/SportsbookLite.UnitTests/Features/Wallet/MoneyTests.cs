using FluentAssertions;
using SportsbookLite.Contracts.Wallet;
using SportsbookLite.TestUtilities;

namespace SportsbookLite.UnitTests.Features.Wallet;

public class MoneyTests : BaseUnitTest
{
    [Fact]
    public void Create_WithValidAmount_ShouldSucceed()
    {
        var amount = 100.50m;
        var currency = "USD";

        var money = Money.Create(amount, currency);

        money.Amount.Should().Be(amount);
        money.Currency.Should().Be(currency);
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrowArgumentException()
    {
        var amount = -50.25m;
        var currency = "USD";

        Action act = () => Money.Create(amount, currency);

        act.Should().Throw<ArgumentException>()
           .WithMessage("Amount cannot be negative (Parameter 'amount')");
    }

    [Fact]
    public void Create_WithZeroAmount_ShouldSucceed()
    {
        var amount = 0m;
        var currency = "USD";

        var money = Money.Create(amount, currency);

        money.Amount.Should().Be(0m);
        money.Currency.Should().Be(currency);
    }

    [Fact]
    public void Zero_WithDefaultCurrency_ShouldReturnUSDZero()
    {
        var money = Money.Zero();

        money.Amount.Should().Be(0m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Zero_WithSpecificCurrency_ShouldReturnZeroInThatCurrency()
    {
        var currency = "EUR";

        var money = Money.Zero(currency);

        money.Amount.Should().Be(0m);
        money.Currency.Should().Be(currency);
    }

    [Fact]
    public void Add_WithSameCurrency_ShouldReturnCorrectSum()
    {
        var money1 = Money.Create(100m);
        var money2 = Money.Create(50.25m);

        var result = money1.Add(money2);

        result.Amount.Should().Be(150.25m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_WithDifferentCurrencies_ShouldThrowInvalidOperationException()
    {
        var usdMoney = Money.Create(100m, "USD");
        var eurMoney = Money.Create(50m, "EUR");

        Action act = () => usdMoney.Add(eurMoney);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Cannot add different currencies: USD and EUR");
    }

    [Fact]
    public void Subtract_WithSameCurrency_ShouldReturnCorrectDifference()
    {
        var money1 = Money.Create(100m);
        var money2 = Money.Create(30.75m);

        var result = money1.Subtract(money2);

        result.Amount.Should().Be(69.25m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Subtract_WithDifferentCurrencies_ShouldThrowInvalidOperationException()
    {
        var usdMoney = Money.Create(100m, "USD");
        var eurMoney = Money.Create(50m, "EUR");

        Action act = () => usdMoney.Subtract(eurMoney);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Cannot subtract different currencies: USD and EUR");
    }

    [Theory]
    [InlineData(100, 50, true)]
    [InlineData(50, 100, false)]
    [InlineData(75, 75, false)]
    public void IsGreaterThan_WithSameCurrency_ShouldReturnCorrectResult(decimal amount1, decimal amount2, bool expectedResult)
    {
        var money1 = Money.Create(amount1);
        var money2 = Money.Create(amount2);

        var result = money1.IsGreaterThan(money2);

        result.Should().Be(expectedResult);
    }

    [Fact]
    public void IsGreaterThan_WithDifferentCurrencies_ShouldThrowInvalidOperationException()
    {
        var usdMoney = Money.Create(100m, "USD");
        var eurMoney = Money.Create(50m, "EUR");

        Action act = () => usdMoney.IsGreaterThan(eurMoney);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Cannot compare different currencies: USD and EUR");
    }

    [Theory]
    [InlineData(100, 50, true)]
    [InlineData(50, 100, false)]
    [InlineData(75, 75, true)]
    public void IsGreaterThanOrEqualTo_WithSameCurrency_ShouldReturnCorrectResult(decimal amount1, decimal amount2, bool expectedResult)
    {
        var money1 = Money.Create(amount1);
        var money2 = Money.Create(amount2);

        var result = money1.IsGreaterThanOrEqualTo(money2);

        result.Should().Be(expectedResult);
    }

    [Fact]
    public void IsGreaterThanOrEqualTo_WithDifferentCurrencies_ShouldThrowInvalidOperationException()
    {
        var usdMoney = Money.Create(100m, "USD");
        var eurMoney = Money.Create(50m, "EUR");

        Action act = () => usdMoney.IsGreaterThanOrEqualTo(eurMoney);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Cannot compare different currencies: USD and EUR");
    }

    [Fact]
    public void Equality_WithSameAmountAndCurrency_ShouldBeEqual()
    {
        var money1 = Money.Create(100.50m, "USD");
        var money2 = Money.Create(100.50m, "USD");

        money1.Should().Be(money2);
        (money1 == money2).Should().BeTrue();
        (money1 != money2).Should().BeFalse();
    }

    [Fact]
    public void Equality_WithDifferentAmount_ShouldNotBeEqual()
    {
        var money1 = Money.Create(100.50m, "USD");
        var money2 = Money.Create(100.51m, "USD");

        money1.Should().NotBe(money2);
        (money1 == money2).Should().BeFalse();
        (money1 != money2).Should().BeTrue();
    }

    [Fact]
    public void Equality_WithDifferentCurrency_ShouldNotBeEqual()
    {
        var money1 = Money.Create(100.50m, "USD");
        var money2 = Money.Create(100.50m, "EUR");

        money1.Should().NotBe(money2);
        (money1 == money2).Should().BeFalse();
        (money1 != money2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_WithSameValues_ShouldReturnSameHashCode()
    {
        var money1 = Money.Create(100.50m, "USD");
        var money2 = Money.Create(100.50m, "USD");

        money1.GetHashCode().Should().Be(money2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WithDifferentValues_ShouldReturnDifferentHashCode()
    {
        var money1 = Money.Create(100.50m, "USD");
        var money2 = Money.Create(100.51m, "USD");

        money1.GetHashCode().Should().NotBe(money2.GetHashCode());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(99.99)]
    [InlineData(1000000)]
    public void ArithmeticOperations_WithValidAmounts_ShouldMaintainPrecision(decimal testAmount)
    {
        var money = Money.Create(testAmount);
        var zero = Money.Zero();

        var additionResult = money.Add(zero);
        var subtractionResult = money.Subtract(zero);

        additionResult.Should().Be(money);
        subtractionResult.Should().Be(money);
    }

    [Fact]
    public void ChainedOperations_ShouldWorkCorrectly()
    {
        var money1 = Money.Create(100m);
        var money2 = Money.Create(50m);
        var money3 = Money.Create(25m);

        var result = money1.Add(money2).Subtract(money3);

        result.Amount.Should().Be(125m);
        result.Currency.Should().Be("USD");
    }
}