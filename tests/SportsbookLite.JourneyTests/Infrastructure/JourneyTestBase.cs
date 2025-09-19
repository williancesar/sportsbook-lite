using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using SportsbookLite.Api;
using SportsbookLite.TestUtilities;

namespace SportsbookLite.JourneyTests.Infrastructure;

public abstract class JourneyTestBase : BaseIntegrationTest
{
    private WebApplicationFactory<Program>? _webAppFactory;
    private HttpClient? _apiClient;
    private TestCluster? _orleansCluster;
    
    protected HttpClient ApiClient => _apiClient ?? throw new InvalidOperationException("API client not initialized");
    protected ITestOutputHelper Output { get; }
    protected TestCluster OrleansCluster => _orleansCluster ?? throw new InvalidOperationException("Orleans cluster not initialized");

    protected JourneyTestBase(ITestOutputHelper output)
    {
        Output = output;
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        
        Output.WriteLine("Initializing Journey Test infrastructure...");
        
        // Setup Orleans test cluster
        await SetupOrleansCluster();
        
        // Setup API with test configuration
        SetupWebApplication();
        
        // Initialize test data if needed
        await InitializeTestDataAsync();
        
        Output.WriteLine("Journey Test infrastructure ready.");
    }

    private async Task SetupOrleansCluster()
    {
        var builder = new TestClusterBuilder();
        
        builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Orleans:ClusterId"] = "journey-test-cluster",
                ["Orleans:ServiceId"] = "journey-test-service",
                ["ConnectionStrings:Database"] = PostgresConnectionString,
                ["ConnectionStrings:Redis"] = RedisConnectionString
            });
        });

        _orleansCluster = builder.Build();
        await _orleansCluster.DeployAsync();
        
        Output.WriteLine($"Orleans cluster deployed with {_orleansCluster.Silos.Count} silo(s)");
    }

    private void SetupWebApplication()
    {
        _webAppFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Database"] = PostgresConnectionString,
                        ["ConnectionStrings:Redis"] = RedisConnectionString,
                        ["Orleans:ClusterId"] = "journey-test-cluster",
                        ["Orleans:ServiceId"] = "journey-test-service",
                        ["Pulsar:ServiceUrl"] = PulsarServiceUrl
                    });
                });
                
                builder.ConfigureServices(services =>
                {
                    // Replace Orleans client with test cluster client
                    services.AddSingleton(_orleansCluster!.Client);
                    
                    // Add test-specific services
                    services.AddSingleton(Output);
                });
            });

        _apiClient = _webAppFactory.CreateClient();
        _apiClient.BaseAddress = new Uri("http://localhost");
        
        Output.WriteLine("Web application factory configured");
    }

    protected virtual async Task InitializeTestDataAsync()
    {
        // Override in derived classes if test data initialization is needed
        await Task.CompletedTask;
    }

    protected async Task<T?> GetFromJsonAsync<T>(string requestUri)
    {
        var response = await ApiClient.GetAsync(requestUri);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }

    protected async Task<TResponse?> PostAsJsonAsync<TRequest, TResponse>(string requestUri, TRequest request)
    {
        var response = await ApiClient.PostAsJsonAsync(requestUri, request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>();
    }

    protected async Task<TResponse?> PutAsJsonAsync<TRequest, TResponse>(string requestUri, TRequest request)
    {
        var response = await ApiClient.PutAsJsonAsync(requestUri, request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>();
    }

    protected async Task DeleteAsync(string requestUri)
    {
        var response = await ApiClient.DeleteAsync(requestUri);
        response.EnsureSuccessStatusCode();
    }

    protected async Task WaitForAsync(Func<Task<bool>> condition, int timeoutMs = 10000, int intervalMs = 100)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            if (await condition())
            {
                return;
            }
            
            await Task.Delay(intervalMs);
        }
        
        throw new TimeoutException($"Condition not met within {timeoutMs}ms");
    }

    protected async Task<T> RetryAsync<T>(Func<Task<T>> operation, int maxAttempts = 3, int delayMs = 1000)
    {
        var attempts = 0;
        Exception? lastException = null;
        
        while (attempts < maxAttempts)
        {
            try
            {
                attempts++;
                return await operation();
            }
            catch (Exception ex)
            {
                lastException = ex;
                Output.WriteLine($"Attempt {attempts} failed: {ex.Message}");
                
                if (attempts < maxAttempts)
                {
                    await Task.Delay(delayMs * attempts); // Exponential backoff
                }
            }
        }
        
        throw new InvalidOperationException($"Operation failed after {maxAttempts} attempts", lastException);
    }

    public override async Task DisposeAsync()
    {
        _apiClient?.Dispose();
        _webAppFactory?.Dispose();
        
        if (_orleansCluster != null)
        {
            await _orleansCluster.DisposeAsync();
        }
        
        await base.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}

public class TestSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.ConfigureServices(services =>
        {
            // Add test-specific services
            services.AddLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Warning); // Reduce noise in tests
                logging.AddConsole();
            });
        });
        
        siloBuilder.UseLocalhostClustering();
        siloBuilder.AddMemoryGrainStorageAsDefault();
        siloBuilder.AddMemoryStreams("Default");
    }
}