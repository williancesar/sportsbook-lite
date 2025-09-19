namespace SportsbookLite.Infrastructure.Pulsar;

public sealed class PulsarOptions
{
    public const string SectionName = "Pulsar";
    
    public string ServiceUrl { get; set; } = "pulsar://localhost:6650";
    
    public string TopicPrefix { get; set; } = "sportsbook.events";
    
    public string DefaultSubscription { get; set; } = "sportsbook-service";
    
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(10);
    
    public bool EnableRetry { get; set; } = true;
    
    public int MaxRetryAttempts { get; set; } = 3;
    
    public bool EnableDeadLetterQueue { get; set; } = true;
    
    public string? AuthenticationToken { get; set; }
    
    public bool EnableTls { get; set; } = false;
    
    public string? TlsCertificatePath { get; set; }
    
    public Dictionary<string, object> ProducerProperties { get; set; } = new();
    
    public Dictionary<string, object> ConsumerProperties { get; set; } = new();
}