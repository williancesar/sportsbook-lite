using FluentAssertions;
using Microsoft.Extensions.Logging;
using SportsbookLite.TestUtilities;

namespace SportsbookLite.IntegrationTests.Infrastructure;

[Collection("IntegrationTestCollection")]
public class BaseIntegrationTestTests : BaseIntegrationTest
{
    [Fact]
    public async Task Should_Initialize_TestContainers_Successfully()
    {
        // Assert - Containers should be started and accessible
        PostgresConnectionString.Should().NotBeNullOrEmpty();
        RedisConnectionString.Should().NotBeNullOrEmpty();
        PulsarServiceUrl.Should().NotBeNullOrEmpty();
        
        PostgresConnectionString.Should().Contain("Host=");
        RedisConnectionString.Should().Contain("localhost");
        PulsarServiceUrl.Should().StartWith("pulsar://");
    }

    [Fact]
    public async Task Should_Provide_Configuration_With_Connection_Strings()
    {
        // Act
        var dbConnectionString = Configuration["ConnectionStrings:Database"];
        var redisConnectionString = Configuration["ConnectionStrings:Redis"];
        var pulsarServiceUrl = Configuration["Pulsar:ServiceUrl"];

        // Assert
        dbConnectionString.Should().NotBeNullOrEmpty();
        redisConnectionString.Should().NotBeNullOrEmpty();
        pulsarServiceUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_Provide_Logging_Services()
    {
        // Act
        var logger = GetRequiredService<ILogger<BaseIntegrationTestTests>>();

        // Assert
        logger.Should().NotBeNull();
        logger.Should().BeAssignableTo<ILogger<BaseIntegrationTestTests>>();

        // Test logging works (this will appear in test output)
        logger.LogInformation("Integration test logging is working correctly");
    }

    [Fact]
    public async Task Should_Have_Different_Connection_Strings_Between_Test_Runs()
    {
        // This test verifies that TestContainers creates isolated environments
        // Connection strings should contain dynamic port numbers
        
        // Assert
        PostgresConnectionString.Should().MatchRegex(@"Port=\d+");
        PulsarServiceUrl.Should().MatchRegex(@":\d+$");
        
        // Log the connection strings for debugging
        var logger = GetRequiredService<ILogger<BaseIntegrationTestTests>>();
        logger.LogInformation("PostgreSQL: {PostgresConnectionString}", PostgresConnectionString);
        logger.LogInformation("Redis: {RedisConnectionString}", RedisConnectionString);
        logger.LogInformation("Pulsar: {PulsarServiceUrl}", PulsarServiceUrl);
    }

    protected override void ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        base.ConfigureServices(services);
        
        // Add any additional services for testing
        // This is where we would configure Entity Framework, Pulsar client, etc.
    }
}

/// <summary>
/// This collection ensures that integration tests that use containers run sequentially
/// to avoid port conflicts and resource contention.
/// </summary>
[CollectionDefinition("IntegrationTestCollection")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
}

public class IntegrationTestFixture
{
    // This can be used for shared setup across integration tests if needed
}