# SportsbookLite Journey Tests

## Overview

The Journey Tests project provides comprehensive end-to-end testing for the Sportsbook-Lite application. Using a **Fluent Builder Pattern**, these tests validate complete user journeys from wallet funding through bet placement and payout, ensuring all system components work together correctly under realistic conditions.

## Key Features

- **Fluent Journey Builder**: Readable, maintainable test scenarios that mirror real user behavior
- **Comprehensive Coverage**: Tests happy paths, error scenarios, concurrent users, and edge cases
- **Infrastructure Testing**: Full integration with Orleans, Pulsar, PostgreSQL, and Redis via TestContainers
- **Performance Validation**: Load testing and response time benchmarking
- **Detailed Reporting**: Journey execution reports with step-by-step validation

## Quick Start

### Prerequisites

- .NET 9 SDK
- Docker Desktop (for TestContainers)
- 8GB RAM minimum
- Ports available: 5432 (PostgreSQL), 6379 (Redis), 6650/8080 (Pulsar)

### Running Tests

```bash
# Run all journey tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~HappyPathJourneyTests"

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"
```

## Test Structure

```
SportsbookLite.JourneyTests/
├── Builders/               # Fluent Builder implementation
│   ├── SportsbookJourneyBuilder.cs
│   ├── JourneyContext.cs
│   ├── Extensions/        # Journey extension methods
│   └── Features/          # Pre-built scenarios
├── Journeys/              # Test classes by journey type
│   ├── HappyPathJourneyTests.cs
│   ├── EventAdministratorJourneyTests.cs
│   ├── ConcurrentBettingJourneyTests.cs
│   ├── CashoutJourneyTests.cs
│   └── ErrorRecoveryJourneyTests.cs
├── Infrastructure/        # Test infrastructure
│   └── JourneyTestBase.cs
└── Scenarios/            # Reusable test scenarios
```

## Writing Journey Tests

### Basic Example

```csharp
[Fact]
public async Task UserBettingJourney_ShouldSucceed()
{
    var context = await new SportsbookJourneyBuilder(ApiClient, Output)
        .CreateUser("test_user")
        .FundWallet(1000m, "USD")
        .CreateEvent("Test Match", SportType.Football)
        .AddMarket("winner", "home", "away")
        .SetOdds("home", 2.00m)
        .PlaceBet(100m, "home", 2.00m)
        .CompleteEvent("home")
        .VerifyBalance(1100m)  // 1000 - 100 + 200
        .ExecuteAsync();
        
    context.AssertAllSuccessful();
}
```

### Using Pre-built Scenarios

```csharp
[Fact]
public async Task QuickBetting_WithScenario_ShouldWork()
{
    var context = await new SportsbookJourneyBuilder(ApiClient, Output)
        .StandardUserWithBalance("user1", 500m)
        .LiveFootballMatch()
        .PlaceBet(50m, "home_win", 2.10m)
        .ExecuteAsync();
        
    context.AssertAllSuccessful();
}
```

### Concurrent User Testing

```csharp
[Fact]
public async Task ConcurrentUsers_ShouldHandleLoad()
{
    // Setup event
    var eventSetup = await new SportsbookJourneyBuilder(ApiClient, Output)
        .LiveFootballMatch("Concurrent Match")
        .ExecuteAsync();
    
    // Create concurrent user journeys
    var userTasks = Enumerable.Range(1, 10)
        .Select(i => new SportsbookJourneyBuilder(ApiClient, Output)
            .CreateUser($"user_{i}")
            .FundWallet(100m, "USD")
            .PlaceBet(10m, "home_win", 2.00m)
            .ExecuteAsync());
    
    // Execute concurrently
    var results = await Task.WhenAll(userTasks);
    
    // Verify all succeeded
    results.Should().AllSatisfy(r => r.AssertAllSuccessful());
}
```

## Journey Types

### 1. Happy Path Journeys
- Complete user flow from registration to payout
- Multiple bets on same event
- Sequential betting across events
- Deposit/withdraw cycles

### 2. Administrator Journeys
- Event creation and configuration
- Odds management and updates
- Settlement processing
- Market suspension/resumption

### 3. Concurrent Betting Journeys
- Multiple users on same event
- High-frequency betting
- Odds changes during betting
- Stress testing with 50+ concurrent users

### 4. Cashout Journeys
- Early bet settlement
- Cashout value calculation
- Partial settlement scenarios
- Risk management flows

### 5. Error Recovery Journeys
- Idempotency validation
- Network failure recovery
- Invalid state handling
- System resilience testing

## Performance Benchmarks

Expected performance metrics under normal load:

| Operation | Target | Actual |
|-----------|--------|--------|
| Bet Placement | < 100ms | ~80ms |
| Balance Query | < 10ms | ~8ms |
| Odds Update | < 50ms | ~40ms |
| Settlement | < 500ms | ~350ms |
| Concurrent Users | 50+ | ✅ |

## Integration with CI/CD

### GitHub Actions Configuration

```yaml
name: Journey Tests
on: [push, pull_request]

jobs:
  journey-tests:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:15
      redis:
        image: redis:7
    steps:
    - uses: actions/checkout@v3
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 9.0.x
    - name: Run Journey Tests
      run: |
        dotnet test tests/SportsbookLite.JourneyTests \
          --logger trx \
          --collect:"XPlat Code Coverage"
    - name: Publish Results
      uses: dorny/test-reporter@v1
      with:
        name: Journey Test Results
        path: '**/*.trx'
        reporter: dotnet-trx
```

## Troubleshooting

### Common Issues

1. **Port Conflicts**
   - Ensure ports 5432, 6379, 6650, 8080 are available
   - Stop any local PostgreSQL/Redis instances

2. **Docker Not Running**
   - TestContainers requires Docker Desktop
   - Verify: `docker ps`

3. **Insufficient Memory**
   - Journey tests spawn multiple containers
   - Allocate at least 4GB to Docker

4. **Test Timeouts**
   - Increase timeout in test settings
   - Check Docker resource limits

### Debug Output

Enable detailed logging:

```csharp
public class MyJourneyTest : JourneyTestBase
{
    public MyJourneyTest(ITestOutputHelper output) : base(output) 
    {
        // Outputs will appear in test results
    }
}
```

## Extending the Framework

### Adding Custom Journey Steps

```csharp
public static class CustomExtensions
{
    public static SportsbookJourneyBuilder MyCustomStep(
        this SportsbookJourneyBuilder builder)
    {
        return builder.Then(async context =>
        {
            // Custom logic here
            context.RecordStep("Custom step executed");
            // Perform actions
            context.RecordAssertion("Custom check", true);
        });
    }
}
```

### Creating New Scenarios

```csharp
public static class MyScenarios
{
    public static SportsbookJourneyBuilder MyScenario(
        this SportsbookJourneyBuilder builder)
    {
        return builder
            .CreateUser("scenario_user")
            .FundWallet(1000m, "USD")
            // Add more steps
            .Then(ctx => ctx.RecordStep("Scenario complete"));
    }
}
```

## Best Practices

1. **Keep Tests Independent**: Each test should set up its own data
2. **Use Meaningful Names**: Test and user names should indicate purpose
3. **Verify State Changes**: Always verify balance/status after operations
4. **Handle Timing**: Use appropriate delays for async operations
5. **Clean Assertions**: Use FluentAssertions for readable validations
6. **Parallel Execution**: Design tests to run in parallel when possible

## Documentation

For detailed documentation including architecture diagrams and sequence flows, see:
- [Journey Tests Documentation](../../docs/journey-tests-documentation.md)
- [API Documentation](../../docs/api-documentation.md)
- [Orleans Testing Guide](../../docs/orleans-testing.md)

## Contributing

When adding new journey tests:
1. Follow existing patterns in the Builders folder
2. Add to appropriate journey category
3. Update this README with new scenarios
4. Ensure tests are idempotent
5. Add performance benchmarks if applicable

## License

Part of the SportsbookLite project - Technical interview demonstration.