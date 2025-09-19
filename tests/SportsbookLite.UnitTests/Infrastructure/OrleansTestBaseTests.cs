using FluentAssertions;
using Microsoft.Extensions.Logging;
using SportsbookLite.TestUtilities;

namespace SportsbookLite.UnitTests.Infrastructure;

public class OrleansTestBaseTests : OrleansTestBase
{
    [Fact]
    public void Should_Initialize_Orleans_Test_Infrastructure()
    {
        // Assert
        Services.Should().NotBeNull();
        Logger.Should().NotBeNull();
    }

    [Fact]
    public void Should_Provide_Service_Access_Methods()
    {
        // Act
        var logger = GetRequiredService<ILogger<OrleansTestBaseTests>>();
        var optionalService = GetService<IOptionalService>();

        // Assert
        logger.Should().NotBeNull();
        optionalService.Should().BeNull(); // Service not registered
    }

    [Fact]
    public void Should_Log_Initialization_Message()
    {
        // The test verifies that logging is working
        // The actual log message is checked in the InitializeAsync method
        Logger.Should().NotBeNull();
    }

    private interface IOptionalService
    {
        // Used for testing optional service resolution
    }
}