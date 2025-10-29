using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace SportsbookLite.TestUtilities;

public abstract class BaseUnitTest : IDisposable
{
    protected ServiceProvider ServiceProvider { get; private set; }
    protected IServiceCollection Services { get; private set; }
    protected ILogger<T> GetLogger<T>() => ServiceProvider.GetRequiredService<ILogger<T>>();

    protected BaseUnitTest()
    {
        Services = new ServiceCollection();
        ConfigureServices(Services);
        ServiceProvider = Services.BuildServiceProvider();
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    }

    protected T GetRequiredService<T>() where T : notnull
        => ServiceProvider.GetRequiredService<T>();

    protected T GetService<T>() where T : class
        => ServiceProvider.GetService<T>()!;

    protected T CreateSubstitute<T>() where T : class
        => Substitute.For<T>();

    protected T CreatePartialSubstitute<T>(params object[] constructorArguments) where T : class
        => Substitute.ForPartsOf<T>(constructorArguments);

    public virtual void Dispose()
    {
        ServiceProvider?.Dispose();
        GC.SuppressFinalize(this);
    }
}