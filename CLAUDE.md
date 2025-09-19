# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Sportsbook-Lite** is a technical interview project demonstrating senior-level expertise in distributed systems using Microsoft Orleans, Apache Pulsar, and event-driven architecture. The project showcases a backend-only sportsbook application built with modern .NET 9 technologies and follows enterprise-grade patterns.

### Purpose
- Demonstrate proficiency for Senior C# Developer position
- Showcase distributed systems expertise with Orleans virtual actors
- Implement event-driven architecture with Apache Pulsar
- Apply clean code principles and modern development practices

## Technology Stack

### Core Requirements (from Job Description)
- **Microsoft Orleans** - Virtual actor model for distributed applications
- **Apache Pulsar** - Message streaming and event processing
- **Event-Driven Architecture (EDA)** - Asynchronous messaging patterns
- **.NET 9 / C# 13** - Latest version with modern features
- **Docker & Kubernetes** - Containerization and orchestration
- **FastEndpoints** - High-performance REST API framework

### Additional Technologies
- **PostgreSQL** - Primary data store
- **Redis** - Orleans clustering and caching
- **xUnit** - Testing framework
- **TestContainers** - Integration testing
- **Serilog** - Structured logging
- **OpenTelemetry** - Distributed tracing

## Architecture & Design Principles

### Vertical Slices Architecture
The project is organized by features (vertical slices) rather than technical layers. Each feature slice is self-contained with minimal cross-slice dependencies.

### Core Principles
- **KISS** (Keep It Simple, Stupid) - Simple, understandable solutions
- **DRY** (Don't Repeat Yourself) - Reusable components and shared contracts
- **YAGNI** (You Aren't Gonna Need It) - Focus on required features only
- **Clean Code** - Readable, maintainable, testable code
- **SOLID** - Object-oriented design principles

### Distributed Systems Patterns
- Event Sourcing for audit trails
- CQRS for read/write separation
- Saga pattern for distributed transactions
- Circuit breaker for fault tolerance
- Bulkhead isolation for resilience

## Project Structure

```
sportsbook-lite/
├── src/
│   ├── SportsbookLite.Host/                 # Orleans Silo Host
│   │   ├── Program.cs                       # Silo configuration
│   │   └── appsettings.json                 # Configuration
│   ├── SportsbookLite.Api/                  # FastEndpoints Web API
│   │   ├── Program.cs                       # API configuration
│   │   └── Endpoints/                       # API endpoints
│   ├── SportsbookLite.Grains/               # Orleans Grain Implementations
│   │   ├── Betting/                         # Bet-related grains
│   │   ├── Events/                          # Event management grains
│   │   ├── Odds/                            # Odds management grains
│   │   └── Wallet/                          # User wallet grains
│   ├── SportsbookLite.GrainInterfaces/      # Orleans Grain Interfaces
│   │   └── Interfaces/                      # Grain contracts
│   ├── SportsbookLite.Infrastructure/       # External Services
│   │   ├── Pulsar/                          # Pulsar integration
│   │   ├── Persistence/                     # Database repositories
│   │   └── Configuration/                   # Infrastructure config
│   ├── SportsbookLite.Contracts/            # Shared DTOs and Models
│   └── Features/                            # Vertical Feature Slices
│       ├── Betting/
│       │   ├── Endpoints/                   # Betting API endpoints
│       │   ├── Grains/                      # Betting grains
│       │   ├── Events/                      # Betting events
│       │   ├── Handlers/                    # Event handlers
│       │   └── Models/                      # Domain models
│       ├── Events/                          # Sport events feature
│       ├── Odds/                            # Odds management feature
│       └── Wallet/                          # Wallet feature
├── tests/
│   ├── SportsbookLite.UnitTests/            # Unit tests
│   ├── SportsbookLite.IntegrationTests/     # Integration tests
│   └── SportsbookLite.TestUtilities/        # Shared test utilities
├── docker/
│   ├── Dockerfile                           # Multi-stage build
│   └── docker-compose.yml                   # Local development
├── k8s/                                     # Kubernetes manifests
│   ├── orleans-cluster.yaml
│   ├── api-deployment.yaml
│   └── pulsar-deployment.yaml
└── scripts/                                 # Development scripts
```

## Development Guidelines

### C# and Orleans Naming Conventions

#### Orleans Grains
```csharp
// Grain interfaces: prefix with 'I', suffix with 'Grain'
public interface IBetGrain : IGrainWithGuidKey { }
public interface IUserWalletGrain : IGrainWithStringKey { }

// Grain implementations: suffix with 'Grain'
[Alias("bet")]  // Use alias for persistence
public sealed class BetGrain : Grain, IBetGrain { }

// Grain state: suffix with 'State'
[GenerateSerializer]
public sealed class BetState { }
```

#### Async/Await Patterns for Distributed Systems
```csharp
// ✅ Good: Parallel calls where possible
public async ValueTask<BetResult> PlaceBetAsync(PlaceBetRequest request)
{
    var userGrain = _grainFactory.GetGrain<IUserGrain>(request.UserId);
    var oddsGrain = _grainFactory.GetGrain<IOddsGrain>(request.MarketId);

    // Parallel execution
    var (balance, currentOdds) = await (
        userGrain.GetBalanceAsync(),
        oddsGrain.GetCurrentOddsAsync()
    );

    // Sequential when dependencies exist
    if (balance < request.Amount)
        return BetResult.Failed("Insufficient funds");

    return await ProcessBetAsync(request, currentOdds);
}

// ❌ Bad: Blocking calls
var balance = userGrain.GetBalanceAsync().Result; // NEVER do this!
```

#### Event Sourcing with Orleans
```csharp
// Domain events
public interface IDomainEvent
{
    Guid Id { get; }
    DateTimeOffset Timestamp { get; }
    string AggregateId { get; }
}

// Event-sourced grain
public sealed class EventSourcedBetGrain : Grain, IEventSourcedGrain<BetAggregate>
{
    private readonly IEventStore _eventStore;
    private BetAggregate? _aggregate;
    
    public async ValueTask<EventSourcedResult> ApplyEventAsync<TEvent>(TEvent domainEvent)
        where TEvent : IDomainEvent
    {
        // Apply event to aggregate
        var result = _aggregate!.Apply(domainEvent);
        if (result.IsSuccess)
        {
            await _eventStore.SaveEventAsync(
                this.GetPrimaryKey().ToString(), 
                domainEvent);
        }
        return result;
    }
}
```

### Apache Pulsar Integration Patterns
```csharp
// Event publisher
public interface IEventPublisher
{
    ValueTask PublishAsync<T>(T eventData) where T : IDomainEvent;
}

// Topic naming convention
"sportsbook.events.{aggregate}.{event-type}"
// Example: sportsbook.events.bet.placed

// Event handler pattern
[EventHandler("betting")]
public sealed class BettingEventHandler : IEventHandler<BetPlacedEvent>
{
    public async ValueTask HandleAsync(BetPlacedEvent domainEvent)
    {
        // Process event
    }
}
```

### Performance Optimization
- Use grain placement strategies (`[PreferLocalPlacement]`, `[OneInstancePerNode]`)
- Implement batch processing for high-volume operations
- Use Redis caching with appropriate TTLs
- Implement circuit breakers for external services
- Use connection pooling for database and Pulsar

## Common Development Commands

### Building the Solution
```bash
# Build the entire solution
dotnet build

# Build in Release mode
dotnet build --configuration Release

# Build specific project
dotnet build src/SportsbookLite.Host/SportsbookLite.Host.csproj
```

### Running the Application
```bash
# Start Orleans Silo
dotnet run --project src/SportsbookLite.Host

# Start API in development
dotnet run --project src/SportsbookLite.Api

# Run with specific environment
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/SportsbookLite.Api
```

### Testing
```bash
# Run all tests
dotnet test

# Run unit tests only
dotnet test tests/SportsbookLite.UnitTests

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "FullyQualifiedName~BetGrainTests.PlaceBet_ValidBet_ShouldReturnSuccess"

# Run tests in parallel
dotnet test --parallel
```

### Docker Commands
```bash
# Build Docker image
docker build -t sportsbook-lite:latest -f docker/Dockerfile .

# Run with Docker Compose (local development)
docker-compose -f docker/docker-compose.yml up -d

# View logs
docker-compose logs -f orleans-silo

# Stop all services
docker-compose down
```

### Orleans Dashboard
```bash
# Access Orleans Dashboard (when running)
# http://localhost:8080/dashboard
```

### Pulsar Management
```bash
# Create topics (in Pulsar container)
docker exec -it pulsar-standalone bin/pulsar-admin topics create persistent://public/default/sportsbook-events

# List topics
docker exec -it pulsar-standalone bin/pulsar-admin topics list public/default

# Monitor topic
docker exec -it pulsar-standalone bin/pulsar-admin topics stats persistent://public/default/sportsbook-events
```

### Database Migrations
```bash
# Add migration
dotnet ef migrations add InitialCreate --project src/SportsbookLite.Infrastructure

# Update database
dotnet ef database update --project src/SportsbookLite.Infrastructure

# Generate SQL script
dotnet ef migrations script --project src/SportsbookLite.Infrastructure
```

## Testing Strategy

### Unit Testing Orleans Grains
```csharp
// Use TestCluster for grain testing
public class BetGrainTests : IAsyncLifetime
{
    private TestCluster _cluster;
    
    public async Task InitializeAsync()
    {
        var builder = new TestClusterBuilder();
        _cluster = builder.Build();
        await _cluster.DeployAsync();
    }
    
    [Fact]
    public async Task PlaceBet_ValidRequest_ShouldSucceed()
    {
        var grain = _cluster.GrainFactory.GetGrain<IBetGrain>(Guid.NewGuid());
        var result = await grain.PlaceBetAsync(new PlaceBetRequest());
        result.Should().NotBeNull();
    }
}
```

### Integration Testing with TestContainers
```csharp
public class IntegrationTestBase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();
    private readonly PulsarContainer _pulsar = new PulsarBuilder().Build();
    
    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _postgres.StartAsync(),
            _pulsar.StartAsync()
        );
    }
}
```

### Test Organization
- Organize tests by feature slice
- Use test data builders for maintainable test data
- Mock external dependencies with NSubstitute
- Test event handlers and sagas separately
- Include performance tests for critical paths

## DevOps & Deployment

### Docker Configuration
```dockerfile
# Multi-stage build for Orleans Silo
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/SportsbookLite.Host -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 11111 30000
ENTRYPOINT ["dotnet", "SportsbookLite.Host.dll"]
```

### Docker Compose for Local Development
```yaml
version: '3.8'
services:
  orleans-silo:
    build: .
    environment:
      - ORLEANS_CLUSTER_ID=dev
      - ConnectionStrings__Database=Host=postgres;Database=sportsbook;Username=dev;Password=dev
      - Pulsar__ServiceUrl=pulsar://pulsar:6650
    depends_on:
      - postgres
      - redis
      - pulsar

  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: sportsbook
      POSTGRES_USER: dev
      POSTGRES_PASSWORD: dev

  redis:
    image: redis:7-alpine

  pulsar:
    image: apachepulsar/pulsar:3.1.0
    command: bin/pulsar standalone
```

### Kubernetes Deployment
```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: orleans-silo
spec:
  serviceName: orleans-silo
  replicas: 3
  selector:
    matchLabels:
      app: orleans-silo
  template:
    metadata:
      labels:
        app: orleans-silo
        orleans/clusterId: sportsbook
        orleans/serviceId: sportsbook-silo
    spec:
      containers:
      - name: silo
        image: sportsbook-lite/silo:latest
        ports:
        - containerPort: 11111
        - containerPort: 30000
        env:
        - name: ORLEANS_SERVICE_ID
          value: "sportsbook-silo"
        - name: ORLEANS_CLUSTER_ID
          value: "sportsbook"
```

### CI/CD Pipeline (GitHub Actions)
```yaml
name: Build and Test
on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 9.0.x
    
    - name: Restore
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Test
      run: dotnet test --no-build --verbosity normal
    
    - name: Build Docker
      run: docker build -t sportsbook-lite:${{ github.sha }} .
```

## Core Features to Implement

### 1. Sport Events Management
- Create, update, and manage sporting events
- Event lifecycle (scheduled → live → completed)
- Event categories and competitions

### 2. Odds Management
- Real-time odds updates via Pulsar
- Odds history tracking
- Automatic suspension during high volatility
- Integration with external odds providers

### 3. Bet Placement
- Bet validation and authorization
- Balance reservation during placement
- Odds locking mechanism
- Idempotent bet operations

### 4. Bet Settlement
- Automatic settlement based on event results
- Batch processing for performance
- Settlement reversal capabilities
- Payout calculations

### 5. User Wallet
- Balance management with double-entry bookkeeping
- Transaction history and audit trail
- Deposit and withdrawal operations
- Real-time balance updates

## Specialized Agents to Use

When working with Claude Code, use these specialized agents for optimal results:

### Development
- **csharp-pro**: Orleans grain implementation, async patterns, C# best practices
- **backend-architect**: System design, API structure, distributed patterns
- **database-optimizer**: Query optimization, index design, Orleans storage

### Testing
- **test-automator**: Orleans TestCluster setup, integration tests
- **debugger**: Troubleshooting grain activation, distributed debugging
- **performance-engineer**: Load testing, grain optimization

### DevOps
- **devops-troubleshooter**: Docker/K8s issues, Orleans clustering
- **deployment-engineer**: CI/CD setup, production deployment
- **cloud-architect**: Azure/AWS infrastructure for Orleans

### Specialized
- **error-detective**: Distributed system debugging, log analysis
- **security-auditor**: API security, authentication/authorization
- **incident-responder**: Production issues with Orleans cluster

## Best Practices Summary

### Orleans-Specific
1. Always use `ValueTask` for grain methods
2. Keep grain state small and serializable
3. Use grain activation/deactivation lifecycle properly
4. Implement idempotency for critical operations
5. Use placement strategies for optimization

### Event-Driven Architecture
1. Design events as immutable records
2. Version events for backward compatibility
3. Implement event replay capabilities
4. Use correlation IDs for tracing
5. Handle out-of-order events gracefully

### Code Quality
1. Write self-documenting code
2. Keep methods small and focused
3. Use dependency injection consistently
4. Implement comprehensive logging
5. Write tests before fixing bugs

### Performance
1. Minimize grain-to-grain communication
2. Use batch operations where possible
3. Implement caching strategically
4. Monitor and profile regularly
5. Design for horizontal scaling

## Troubleshooting Common Issues

### Orleans Cluster Not Forming
- Check network connectivity between silos
- Verify clustering provider configuration
- Ensure consistent cluster ID across silos

### Grain Activation Failures
- Check grain interface registration
- Verify storage provider configuration
- Review grain constructor dependencies

### Pulsar Connection Issues
- Verify Pulsar service URL
- Check network policies in Kubernetes
- Ensure topics are created

### Performance Problems
- Review grain placement strategies
- Check for grain hot spots
- Analyze database query performance
- Monitor Pulsar consumer lag

## Additional Resources

### Documentation
- [Microsoft Orleans Documentation](https://docs.microsoft.com/en-us/dotnet/orleans/)
- [Apache Pulsar Documentation](https://pulsar.apache.org/docs/)
- [FastEndpoints Documentation](https://fast-endpoints.com/)

### Monitoring
- Orleans Dashboard: `http://localhost:8080/dashboard`
- Pulsar Manager: `http://localhost:8527`
- Application Insights / Prometheus + Grafana

### Development Tools
- Visual Studio 2022 / JetBrains Rider
- Docker Desktop
- Kubernetes Dashboard / k8s
- Postman / Insomnia for API testing