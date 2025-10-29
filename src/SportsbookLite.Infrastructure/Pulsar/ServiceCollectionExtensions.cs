using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SportsbookLite.Infrastructure.Pulsar;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPulsarServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PulsarOptions>(configuration.GetSection(PulsarOptions.SectionName));
        
        services.AddSingleton<IPulsarService, PulsarService>();
        services.AddSingleton<OddsEventPublisher>();
        
        services.AddSingleton<IHostedService, OddsUpdateConsumer>();
        
        return services;
    }
    
    public static IServiceCollection AddPulsarServices(this IServiceCollection services, Action<PulsarOptions> configurePulsar)
    {
        services.Configure(configurePulsar);
        
        services.AddSingleton<IPulsarService, PulsarService>();
        services.AddSingleton<OddsEventPublisher>();
        
        services.AddSingleton<IHostedService, OddsUpdateConsumer>();
        
        return services;
    }
}