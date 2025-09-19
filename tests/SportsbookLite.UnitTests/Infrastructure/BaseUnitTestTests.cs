using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SportsbookLite.TestUtilities;
using SportsbookLite.TestUtilities.TestDataBuilders;

namespace SportsbookLite.UnitTests.Infrastructure;

public class BaseUnitTestTests : BaseUnitTest
{
    [Fact]
    public void Should_Initialize_Service_Provider()
    {
        // Arrange & Act
        var serviceProvider = ServiceProvider;

        // Assert
        serviceProvider.Should().NotBeNull();
    }

    [Fact]
    public void Should_Provide_Logger_Service()
    {
        // Act
        var logger = GetLogger<BaseUnitTestTests>();

        // Assert
        logger.Should().NotBeNull();
        logger.Should().BeAssignableTo<ILogger<BaseUnitTestTests>>();
    }

    [Fact]
    public void Should_Create_Substitutes_Using_NSubstitute()
    {
        // Act
        var mockService = CreateSubstitute<ITestService>();
        mockService.GetValue().Returns("mocked value");

        // Assert
        mockService.Should().NotBeNull();
        mockService.GetValue().Should().Be("mocked value");
    }

    [Fact]
    public void Should_Generate_Test_Data_Using_Bogus()
    {
        // Act
        var userId = CommonTestData.Identifiers.UserId;
        var amount = CommonTestData.Financial.Amount;
        var teamName = CommonTestData.Sports.TeamName;

        // Assert
        userId.Should().NotBe(Guid.Empty);
        amount.Should().BeGreaterThan(0);
        teamName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Should_Use_FluentAssertions_For_Better_Test_Readability()
    {
        // Arrange
        var testValue = "Hello World";
        var testNumber = 42;
        var testList = new List<int> { 1, 2, 3 };

        // Assert - Demonstrating FluentAssertions syntax
        testValue.Should().NotBeNullOrEmpty()
            .And.StartWith("Hello")
            .And.EndWith("World")
            .And.HaveLength(11);

        testNumber.Should().Be(42)
            .And.BeGreaterThan(40)
            .And.BeLessThan(50);

        testList.Should().HaveCount(3)
            .And.Contain(2)
            .And.BeInAscendingOrder();
    }

    public interface ITestService
    {
        string GetValue();
    }
}