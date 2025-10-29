using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace SportsbookLite.TestUtilities;

/// <summary>
/// Base class for Orleans grain testing using TestCluster.
/// Note: This is a simplified version for Phase 1.4. 
/// Full Orleans TestCluster configuration will be implemented when grains are created.
/// </summary>
public abstract class OrleansTestBase : IAsyncLifetime
{
    protected IServiceProvider Services { get; private set; } = null!;
    protected ILogger Logger { get; private set; } = null!;

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
    }

    public virtual Task InitializeAsync()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
        Logger = Services.GetRequiredService<ILogger<OrleansTestBase>>();
        
        Logger.LogInformation("Orleans test base initialized. Full TestCluster setup will be implemented with grain creation.");
        return Task.CompletedTask;
    }

    public virtual Task DisposeAsync()
    {
        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }
        
        return Task.CompletedTask;
    }

    protected T GetRequiredService<T>() where T : notnull 
        => Services.GetRequiredService<T>();

    protected T? GetService<T>() where T : class 
        => Services.GetService<T>();
}