using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Testcontainers.Pulsar;
using Xunit;

namespace SportsbookLite.TestUtilities;

public abstract class BaseIntegrationTest : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RedisContainer _redisContainer;
    private readonly PulsarContainer _pulsarContainer;

    protected ServiceProvider ServiceProvider { get; private set; } = null!;
    protected IConfiguration Configuration { get; private set; } = null!;

    protected string PostgresConnectionString => _postgresContainer.GetConnectionString();
    protected string RedisConnectionString => _redisContainer.GetConnectionString();
    protected string PulsarServiceUrl => $"pulsar://{_pulsarContainer.Hostname}:{_pulsarContainer.GetMappedPublicPort(6650)}";

    protected BaseIntegrationTest()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .WithDatabase("sportsbook_test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithCleanUp(true)
            .Build();

        _pulsarContainer = new PulsarBuilder()
            .WithImage("apachepulsar/pulsar:3.1.0")
            .WithCleanUp(true)
            .Build();
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
    }

    protected virtual IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = PostgresConnectionString,
                ["ConnectionStrings:Redis"] = RedisConnectionString,
                ["Pulsar:ServiceUrl"] = PulsarServiceUrl,
                ["Orleans:ClusterId"] = "test-cluster",
                ["Orleans:ServiceId"] = "sportsbook-test"
            })
            .Build();
    }

    public virtual async Task InitializeAsync()
    {
        // Start all containers in parallel for faster test setup
        await Task.WhenAll(
            _postgresContainer.StartAsync(),
            _redisContainer.StartAsync(),
            _pulsarContainer.StartAsync()
        );

        // Build configuration after containers are started
        Configuration = BuildConfiguration();

        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        // Additional initialization can be performed by derived classes
        await OnInitializedAsync();
    }

    protected virtual Task OnInitializedAsync() => Task.CompletedTask;

    public virtual async Task DisposeAsync()
    {
        ServiceProvider?.Dispose();

        // Stop all containers in parallel
        await Task.WhenAll(
            _postgresContainer.StopAsync(),
            _redisContainer.StopAsync(),
            _pulsarContainer.StopAsync()
        );
    }

    protected T GetRequiredService<T>() where T : notnull 
        => ServiceProvider.GetRequiredService<T>();

    protected T? GetService<T>() where T : class 
        => ServiceProvider.GetService<T>();
}