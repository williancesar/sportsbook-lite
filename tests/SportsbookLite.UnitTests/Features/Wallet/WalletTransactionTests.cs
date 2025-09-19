using FluentAssertions;
using SportsbookLite.Contracts.Wallet;
using SportsbookLite.TestUtilities;

namespace SportsbookLite.UnitTests.Features.Wallet;

public class WalletTransactionTests : BaseUnitTest
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateTransaction()
    {
        var userId = "user123";
        var type = TransactionType.Deposit;
        var amount = Money.Create(100m);
        var description = "Test deposit";
        var referenceId = "ref123";

        var transaction = WalletTransaction.Create(userId, type, amount, description, referenceId);

        transaction.Id.Should().NotBeNullOrEmpty();
        transaction.UserId.Should().Be(userId);
        transaction.Type.Should().Be(type);
        transaction.Amount.Should().Be(amount);
        transaction.Status.Should().Be(TransactionStatus.Pending);
        transaction.Description.Should().Be(description);
        transaction.ReferenceId.Should().Be(referenceId);
        transaction.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        transaction.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Create_WithoutReferenceId_ShouldCreateTransactionWithNullReference()
    {
        var userId = "user124";
        var type = TransactionType.Withdrawal;
        var amount = Money.Create(50.25m);
        var description = "Test withdrawal";

        var transaction = WalletTransaction.Create(userId, type, amount, description);

        transaction.Id.Should().NotBeNullOrEmpty();
        transaction.UserId.Should().Be(userId);
        transaction.Type.Should().Be(type);
        transaction.Amount.Should().Be(amount);
        transaction.Status.Should().Be(TransactionStatus.Pending);
        transaction.Description.Should().Be(description);
        transaction.ReferenceId.Should().BeNull();
        transaction.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void WithStatus_ToCompleted_ShouldUpdateStatus()
    {
        var transaction = WalletTransaction.Create(
            "user125",
            TransactionType.BetPlacement,
            Money.Create(25m),
            "Bet placement");

        var updatedTransaction = transaction.WithStatus(TransactionStatus.Completed);

        updatedTransaction.Status.Should().Be(TransactionStatus.Completed);
        updatedTransaction.ErrorMessage.Should().BeNull();
        updatedTransaction.Id.Should().Be(transaction.Id);
        updatedTransaction.UserId.Should().Be(transaction.UserId);
        updatedTransaction.Type.Should().Be(transaction.Type);
        updatedTransaction.Amount.Should().Be(transaction.Amount);
        updatedTransaction.Description.Should().Be(transaction.Description);
        updatedTransaction.ReferenceId.Should().Be(transaction.ReferenceId);
        updatedTransaction.Timestamp.Should().Be(transaction.Timestamp);
    }

    [Fact]
    public void WithStatus_ToFailed_ShouldUpdateStatusAndErrorMessage()
    {
        var transaction = WalletTransaction.Create(
            "user126",
            TransactionType.Deposit,
            Money.Create(100m),
            "Failed deposit");
        var errorMessage = "Insufficient funds";

        var updatedTransaction = transaction.WithStatus(TransactionStatus.Failed, errorMessage);

        updatedTransaction.Status.Should().Be(TransactionStatus.Failed);
        updatedTransaction.ErrorMessage.Should().Be(errorMessage);
        updatedTransaction.Id.Should().Be(transaction.Id);
        updatedTransaction.UserId.Should().Be(transaction.UserId);
        updatedTransaction.Type.Should().Be(transaction.Type);
        updatedTransaction.Amount.Should().Be(transaction.Amount);
        updatedTransaction.Description.Should().Be(transaction.Description);
        updatedTransaction.ReferenceId.Should().Be(transaction.ReferenceId);
        updatedTransaction.Timestamp.Should().Be(transaction.Timestamp);
    }

    [Fact]
    public void WithStatus_ToCancelled_ShouldUpdateStatus()
    {
        var transaction = WalletTransaction.Create(
            "user127",
            TransactionType.Reservation,
            Money.Create(75m),
            "Bet reservation");

        var updatedTransaction = transaction.WithStatus(TransactionStatus.Cancelled);

        updatedTransaction.Status.Should().Be(TransactionStatus.Cancelled);
        updatedTransaction.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(TransactionType.Deposit)]
    [InlineData(TransactionType.Withdrawal)]
    [InlineData(TransactionType.BetPlacement)]
    [InlineData(TransactionType.BetWin)]
    [InlineData(TransactionType.BetLoss)]
    [InlineData(TransactionType.BetRefund)]
    [InlineData(TransactionType.Reservation)]
    [InlineData(TransactionType.ReservationCommit)]
    [InlineData(TransactionType.ReservationRelease)]
    public void Create_WithAllTransactionTypes_ShouldCreateCorrectly(TransactionType transactionType)
    {
        var userId = "user128";
        var amount = Money.Create(50m);
        var description = $"Test {transactionType}";

        var transaction = WalletTransaction.Create(userId, transactionType, amount, description);

        transaction.Type.Should().Be(transactionType);
        transaction.Status.Should().Be(TransactionStatus.Pending);
    }

    [Theory]
    [InlineData(TransactionStatus.Pending)]
    [InlineData(TransactionStatus.Completed)]
    [InlineData(TransactionStatus.Failed)]
    [InlineData(TransactionStatus.Cancelled)]
    public void WithStatus_WithAllStatuses_ShouldUpdateCorrectly(TransactionStatus status)
    {
        var transaction = WalletTransaction.Create(
            "user129",
            TransactionType.Deposit,
            Money.Create(100m),
            "Status test");

        var updatedTransaction = transaction.WithStatus(status);

        updatedTransaction.Status.Should().Be(status);
    }

    [Fact]
    public void Equality_WithSameValues_ShouldBeEqual()
    {
        var userId = "user130";
        var type = TransactionType.Deposit;
        var amount = Money.Create(100m);
        var description = "Test";
        var referenceId = "ref123";

        var transaction1 = WalletTransaction.Create(userId, type, amount, description, referenceId);
        var transaction2 = transaction1 with { };

        transaction1.Should().Be(transaction2);
        (transaction1 == transaction2).Should().BeTrue();
        (transaction1 != transaction2).Should().BeFalse();
    }

    [Fact]
    public void Equality_WithDifferentId_ShouldNotBeEqual()
    {
        var userId = "user131";
        var type = TransactionType.Deposit;
        var amount = Money.Create(100m);
        var description = "Test";

        var transaction1 = WalletTransaction.Create(userId, type, amount, description);
        var transaction2 = WalletTransaction.Create(userId, type, amount, description);

        transaction1.Should().NotBe(transaction2);
        (transaction1 == transaction2).Should().BeFalse();
        (transaction1 != transaction2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_WithSameTransaction_ShouldReturnSameHashCode()
    {
        var transaction1 = WalletTransaction.Create(
            "user132",
            TransactionType.Withdrawal,
            Money.Create(50m),
            "Hash test");
        var transaction2 = transaction1 with { };

        transaction1.GetHashCode().Should().Be(transaction2.GetHashCode());
    }

    [Fact]
    public void MultipleStatusUpdates_ShouldCreateNewInstances()
    {
        var originalTransaction = WalletTransaction.Create(
            "user133",
            TransactionType.BetPlacement,
            Money.Create(25m),
            "Multi-status test");

        var completedTransaction = originalTransaction.WithStatus(TransactionStatus.Completed);
        var failedFromCompleted = completedTransaction.WithStatus(TransactionStatus.Failed, "Error occurred");

        originalTransaction.Status.Should().Be(TransactionStatus.Pending);
        completedTransaction.Status.Should().Be(TransactionStatus.Completed);
        failedFromCompleted.Status.Should().Be(TransactionStatus.Failed);
        failedFromCompleted.ErrorMessage.Should().Be("Error occurred");

        originalTransaction.Id.Should().Be(completedTransaction.Id);
        completedTransaction.Id.Should().Be(failedFromCompleted.Id);
    }

    [Fact]
    public void Transaction_ShouldBeImmutable()
    {
        var transaction = WalletTransaction.Create(
            "user134",
            TransactionType.Deposit,
            Money.Create(100m),
            "Immutability test");

        var modifiedTransaction = transaction.WithStatus(TransactionStatus.Completed);

        transaction.Status.Should().Be(TransactionStatus.Pending);
        modifiedTransaction.Status.Should().Be(TransactionStatus.Completed);
        transaction.Should().NotBe(modifiedTransaction);
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldStillCreateTransaction()
    {
        var transaction = WalletTransaction.Create(
            string.Empty,
            TransactionType.Deposit,
            Money.Create(100m),
            "Empty user test");

        transaction.UserId.Should().BeEmpty();
        transaction.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Create_WithEmptyDescription_ShouldStillCreateTransaction()
    {
        var transaction = WalletTransaction.Create(
            "user135",
            TransactionType.Withdrawal,
            Money.Create(50m),
            string.Empty);

        transaction.Description.Should().BeEmpty();
        transaction.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Create_WithZeroAmount_ShouldCreateTransaction()
    {
        var transaction = WalletTransaction.Create(
            "user136",
            TransactionType.BetRefund,
            Money.Zero(),
            "Zero amount test");

        transaction.Amount.Should().Be(Money.Zero());
        transaction.Id.Should().NotBeNullOrEmpty();
    }
}