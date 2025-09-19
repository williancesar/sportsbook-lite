# Sportsbook-Lite Journey Tests Documentation

## Overview

The Journey Tests project provides comprehensive end-to-end testing for the Sportsbook-Lite application using a Fluent Builder pattern. These tests validate complete user journeys from wallet funding through bet placement and payout, ensuring all system components work together correctly.

## Architecture

### Fluent Journey Builder Pattern

The core of the testing framework is the `SportsbookJourneyBuilder` class, which provides a fluent, readable API for constructing complex test scenarios:

```csharp
var context = await new SportsbookJourneyBuilder(client, output)
    .CreateUser("john_doe")
    .FundWallet(1000m, "USD")
    .CreateEvent("Premier League Final", SportType.Football)
    .PlaceBet(100m, "home_win", 2.10m)
    .CompleteEvent("home_win")
    .VerifyBalance(1110m)
    .ExecuteAsync();
```

## User Journey Flow Diagrams

### 1. Happy Path User Journey

```mermaid
graph TD
    Start[Start Journey] --> CreateUser[Create User]
    CreateUser --> FundWallet[Fund Wallet $1000]
    FundWallet --> BrowseEvents[Browse Available Events]
    BrowseEvents --> CheckOdds[Check Odds for Selection]
    CheckOdds --> PlaceBet[Place Bet $100]
    PlaceBet --> VerifyBalance1[Verify Balance: $900]
    VerifyBalance1 --> WaitEvent[Wait for Event Start]
    WaitEvent --> EventLive[Event Goes Live]
    EventLive --> EventComplete[Event Completes]
    EventComplete --> Settlement[Bet Settlement Processing]
    Settlement --> CheckResult{Bet Result?}
    CheckResult -->|Won| Payout[Receive Payout $210]
    CheckResult -->|Lost| NoP payout[No Payout]
    Payout --> VerifyBalance2[Verify Final Balance: $1110]
    NoPayout --> VerifyBalance3[Verify Final Balance: $900]
    
    style Start fill:#e1f5e1
    style VerifyBalance2 fill:#e1f5e1
    style VerifyBalance3 fill:#ffe1e1
```

### 2. Event Administrator Journey

```mermaid
graph TD
    Start[Admin Login] --> CreateEvent[Create Sport Event]
    CreateEvent --> AddMarkets[Add Betting Markets]
    AddMarkets --> SetOdds[Set Initial Odds]
    SetOdds --> PublishEvent[Publish Event]
    PublishEvent --> EventScheduled[Event Status: Scheduled]
    EventScheduled --> StartEvent[Start Event]
    StartEvent --> EventLive[Event Status: Live]
    EventLive --> MonitorVolatility[Monitor Odds Volatility]
    MonitorVolatility --> Decision{High Volatility?}
    Decision -->|Yes| SuspendOdds[Suspend Odds]
    Decision -->|No| ContinueMonitor[Continue Monitoring]
    SuspendOdds --> UpdateOdds[Update Odds]
    UpdateOdds --> ResumeOdds[Resume Odds]
    ResumeOdds --> ContinueMonitor
    ContinueMonitor --> CompleteEvent[Complete Event]
    CompleteEvent --> SetResults[Set Match Results]
    SetResults --> TriggerSettlement[Trigger Settlement]
    TriggerSettlement --> VerifySettlement[Verify All Bets Settled]
    
    style Start fill:#fff4e1
    style VerifySettlement fill:#e1f5e1
```

### 3. Cashout Journey

```mermaid
graph TD
    Start[User Has Active Bet] --> MonitorOdds[Monitor Live Odds]
    MonitorOdds --> OddsChange{Odds Changed?}
    OddsChange -->|Favorable| CalcCashout[Calculate Cashout Value]
    OddsChange -->|Unfavorable| Wait[Wait/Monitor]
    Wait --> MonitorOdds
    CalcCashout --> ShowValue[Display Cashout Value]
    ShowValue --> UserDecision{Accept Cashout?}
    UserDecision -->|Yes| ProcessCashout[Process Cashout]
    UserDecision -->|No| ContinueBet[Continue with Bet]
    ProcessCashout --> DeductFee[Deduct 5% Fee]
    DeductFee --> CreditWallet[Credit Wallet]
    CreditWallet --> CloseBet[Close Bet as Cashed Out]
    CloseBet --> UpdateBalance[Update Balance]
    ContinueBet --> WaitResult[Wait for Event Result]
    WaitResult --> NormalSettlement[Normal Settlement]
    
    style Start fill:#e1e5f5
    style UpdateBalance fill:#e1f5e1
    style NormalSettlement fill:#f5f5e1
```

## Sequence Diagrams

### Bet Placement Sequence

```mermaid
sequenceDiagram
    participant Test as Journey Test
    participant Builder as Journey Builder
    participant API as FastEndpoints API
    participant Orleans as Orleans Grain
    participant DB as PostgreSQL
    participant Pulsar as Apache Pulsar
    
    Test->>Builder: PlaceBet(100, "home_win")
    Builder->>API: POST /api/bets
    API->>Orleans: GetGrain<IBetGrain>
    Orleans->>Orleans: ValidateBet()
    Orleans->>Orleans: GetGrain<IWalletGrain>
    Orleans->>Orleans: ReserveBalance(100)
    Orleans->>DB: Save Bet State
    Orleans->>Pulsar: Publish BetPlacedEvent
    Orleans-->>API: BetResponse
    API-->>Builder: HTTP 200 + BetId
    Builder->>Builder: Update Context
    Builder-->>Test: Context with BetId
    
    Note over Orleans: Grain maintains state in memory
    Note over Pulsar: Event triggers downstream processing
```

### Concurrent Betting Synchronization

```mermaid
sequenceDiagram
    participant U1 as User 1
    participant U2 as User 2
    participant U3 as User 3
    participant API as API
    participant Odds as OddsGrain
    participant Bet as BetGrain
    
    par Parallel Requests
        U1->>API: Place Bet
        U2->>API: Place Bet
        U3->>API: Place Bet
    end
    
    API->>Odds: Lock Odds
    Note over Odds: Orleans ensures single-threaded access
    
    API->>Bet: Process User1 Bet
    Bet-->>API: Bet1 Accepted
    API-->>U1: Success
    
    API->>Bet: Process User2 Bet
    Bet-->>API: Bet2 Accepted
    API-->>U2: Success
    
    API->>Bet: Process User3 Bet
    Bet-->>API: Bet3 Accepted
    API-->>U3: Success
    
    API->>Odds: Unlock Odds
    
    Note over Odds: Odds remain consistent during betting
```

### Settlement Saga Pattern

```mermaid
sequenceDiagram
    participant Event as EventGrain
    participant Saga as SettlementSaga
    participant Bet1 as BetGrain1
    participant Bet2 as BetGrain2
    participant Wallet as WalletGrain
    participant Pulsar as Pulsar
    
    Event->>Saga: StartSettlement(eventId, results)
    Saga->>Saga: Initialize Saga State
    
    Saga->>Bet1: GetBetDetails()
    Bet1-->>Saga: BetDetails
    Saga->>Bet1: SettleBet(result)
    Bet1->>Wallet: CreditWinnings()
    Wallet-->>Bet1: Success
    Bet1-->>Saga: Settled
    
    Saga->>Bet2: GetBetDetails()
    Bet2-->>Saga: BetDetails
    Saga->>Bet2: SettleBet(result)
    Note over Bet2: This bet lost
    Bet2-->>Saga: Settled
    
    Saga->>Pulsar: Publish SettlementCompleted
    Saga->>Saga: Mark Saga Complete
    
    Note over Saga: Saga ensures all-or-nothing settlement
```

## State Diagrams

### Bet Lifecycle States

```mermaid
stateDiagram-v2
    [*] --> Pending: Bet Placed
    Pending --> Validating: Validate Request
    Validating --> Rejected: Invalid
    Validating --> Accepted: Valid
    Accepted --> Reserved: Funds Reserved
    Reserved --> Live: Event Started
    
    Live --> CashoutAvailable: Odds Changed
    CashoutAvailable --> CashoutRequested: User Action
    CashoutRequested --> CashedOut: Process Cashout
    
    Live --> Settling: Event Completed
    Settling --> Won: Selection Correct
    Settling --> Lost: Selection Wrong
    Settling --> Void: Event Cancelled
    
    Won --> Paid: Payout Processed
    Paid --> [*]
    Lost --> [*]
    CashedOut --> [*]
    Void --> Refunded: Return Stake
    Refunded --> [*]
    Rejected --> [*]
    
    Note right of Live: Can transition to\nCashout or Settlement
    Note right of Settling: Final state transition\nbased on results
```

### Event Lifecycle States

```mermaid
stateDiagram-v2
    [*] --> Created: Create Event
    Created --> Configured: Add Markets & Odds
    Configured --> Scheduled: Ready for Betting
    Scheduled --> Live: Start Event
    
    Live --> Suspended: High Volatility/Issue
    Suspended --> Live: Resume
    
    Live --> Completing: Event Ending
    Completing --> Completed: Results Set
    Completed --> Settled: All Bets Processed
    
    Scheduled --> Cancelled: Cancel Event
    Live --> Cancelled: Emergency Cancel
    Cancelled --> Refunding: Process Refunds
    Refunding --> RefundComplete: All Refunds Done
    
    Settled --> [*]
    RefundComplete --> [*]
    
    Note right of Live: Most time spent here
    Note right of Cancelled: Triggers refund saga
```

### Wallet Transaction States

```mermaid
stateDiagram-v2
    [*] --> Initiated: Transaction Request
    Initiated --> Validating: Check Parameters
    Validating --> Rejected: Invalid Request
    Validating --> Processing: Valid Request
    
    Processing --> Pending: Await Confirmation
    Pending --> Confirmed: Transaction Confirmed
    Pending --> Failed: Transaction Failed
    
    Confirmed --> Completed: Update Balance
    Failed --> Reversed: Rollback Changes
    
    Completed --> [*]
    Reversed --> [*]
    Rejected --> [*]
    
    Note right of Processing: Double-entry bookkeeping
    Note right of Reversed: Maintains consistency
```

## Test Infrastructure Architecture

```mermaid
graph TB
    subgraph "Journey Test Infrastructure"
        TestRunner[xUnit Test Runner]
        JourneyBuilder[Journey Builder]
        Context[Journey Context]
    end
    
    subgraph "Test Containers"
        Postgres[(PostgreSQL)]
        Redis[(Redis)]
        Pulsar[Apache Pulsar]
    end
    
    subgraph "Application Under Test"
        API[FastEndpoints API]
        Orleans[Orleans Test Cluster]
        Grains[Grain Instances]
    end
    
    TestRunner --> JourneyBuilder
    JourneyBuilder --> Context
    JourneyBuilder --> API
    
    API --> Orleans
    Orleans --> Grains
    
    Grains --> Postgres
    Orleans --> Redis
    Grains --> Pulsar
    
    style TestRunner fill:#e1f5e1
    style JourneyBuilder fill:#e1e5f5
    style API fill:#fff4e1
```

## Journey Builder Class Hierarchy

```mermaid
classDiagram
    class SportsbookJourneyBuilder {
        -HttpClient client
        -List~JourneyStep~ steps
        -ITestOutputHelper output
        +CreateUser(userId) SportsbookJourneyBuilder
        +FundWallet(amount, currency) SportsbookJourneyBuilder
        +CreateEvent(name, sport) SportsbookJourneyBuilder
        +PlaceBet(stake, selection) SportsbookJourneyBuilder
        +CompleteEvent(result) SportsbookJourneyBuilder
        +VerifyBalance(expected) SportsbookJourneyBuilder
        +ExecuteAsync() JourneyContext
    }
    
    class JourneyContext {
        -Dictionary~string,object~ data
        -List~AssertionResult~ assertions
        -List~string~ executedSteps
        +Set(key, value) void
        +Get(key) T
        +RecordStep(name) void
        +RecordAssertion(desc, success) void
        +AssertAllSuccessful() void
        +GetJourneyReport() string
    }
    
    class JourneyStep {
        <<interface>>
        +ExecuteAsync(context) Task
        +Validate() bool
    }
    
    class WalletStep {
        +ExecuteAsync(context) Task
        +Validate() bool
    }
    
    class BettingStep {
        +ExecuteAsync(context) Task
        +Validate() bool
    }
    
    class AssertionStep {
        +ExecuteAsync(context) Task
        +Validate() bool
    }
    
    SportsbookJourneyBuilder --> JourneyContext : creates
    SportsbookJourneyBuilder --> JourneyStep : contains
    JourneyStep <|-- WalletStep : implements
    JourneyStep <|-- BettingStep : implements
    JourneyStep <|-- AssertionStep : implements
```

## Data Flow Through System

```mermaid
graph LR
    subgraph "Input Layer"
        User[User Action]
        Admin[Admin Action]
    end
    
    subgraph "API Layer"
        Endpoint[FastEndpoint]
        Validator[Request Validator]
        Auth[Authorization]
    end
    
    subgraph "Orleans Layer"
        Router[Grain Router]
        BetGrain[Bet Grain]
        WalletGrain[Wallet Grain]
        EventGrain[Event Grain]
        OddsGrain[Odds Grain]
    end
    
    subgraph "Persistence Layer"
        GrainState[Grain State]
        EventStore[Event Store]
        Snapshots[Snapshots]
    end
    
    subgraph "Messaging Layer"
        Publisher[Event Publisher]
        Topics[Pulsar Topics]
        Consumers[Event Consumers]
    end
    
    User --> Endpoint
    Admin --> Endpoint
    Endpoint --> Validator
    Validator --> Auth
    Auth --> Router
    
    Router --> BetGrain
    Router --> WalletGrain
    Router --> EventGrain
    Router --> OddsGrain
    
    BetGrain --> GrainState
    WalletGrain --> GrainState
    EventGrain --> EventStore
    OddsGrain --> Snapshots
    
    BetGrain --> Publisher
    EventGrain --> Publisher
    Publisher --> Topics
    Topics --> Consumers
    
    style User fill:#e1f5e1
    style Admin fill:#fff4e1
    style Router fill:#e1e5f5
```

## Test Execution Flow

```mermaid
flowchart TD
    Start[Test Starts] --> Init[Initialize Infrastructure]
    Init --> Containers[Start TestContainers]
    Containers --> Orleans[Deploy Orleans Cluster]
    Orleans --> API[Start Web API]
    API --> Builder[Create Journey Builder]
    
    Builder --> Steps[Add Journey Steps]
    Steps --> Execute[Execute Journey]
    
    Execute --> Step1[Execute Step 1]
    Step1 --> Record1[Record Result]
    Record1 --> Step2[Execute Step 2]
    Step2 --> Record2[Record Result]
    Record2 --> StepN[Execute Step N]
    StepN --> RecordN[Record Result]
    
    RecordN --> Assertions[Run Assertions]
    Assertions --> Report[Generate Report]
    
    Report --> Success{All Passed?}
    Success -->|Yes| Pass[Test Passes]
    Success -->|No| Fail[Test Fails]
    
    Pass --> Cleanup[Cleanup Resources]
    Fail --> Cleanup
    Cleanup --> End[Test Ends]
    
    style Start fill:#e1f5e1
    style Pass fill:#e1f5e1
    style Fail fill:#ffe1e1
    style End fill:#f5f5f5
```

## Performance Testing Strategy

```mermaid
graph TD
    subgraph "Load Generation"
        Users[Virtual Users]
        Scenarios[Test Scenarios]
        Load[Load Pattern]
    end
    
    subgraph "System Under Test"
        API[API Layer]
        Orleans[Orleans Cluster]
        DB[Database]
        Cache[Redis Cache]
    end
    
    subgraph "Monitoring"
        Metrics[Performance Metrics]
        Logs[Application Logs]
        Traces[Distributed Traces]
    end
    
    subgraph "Analysis"
        Response[Response Times]
        Throughput[Throughput]
        Errors[Error Rates]
        Bottlenecks[Bottlenecks]
    end
    
    Users --> API
    Scenarios --> API
    Load --> API
    
    API --> Orleans
    Orleans --> DB
    Orleans --> Cache
    
    API --> Metrics
    Orleans --> Logs
    DB --> Traces
    
    Metrics --> Response
    Logs --> Throughput
    Traces --> Errors
    Response --> Bottlenecks
    Throughput --> Bottlenecks
    Errors --> Bottlenecks
```

## Key Testing Patterns

### 1. Idempotency Testing
Tests verify that duplicate requests with the same idempotency key return the same result without side effects.

### 2. Concurrent User Simulation
Multiple virtual users perform actions simultaneously to test system concurrency handling.

### 3. State Verification
After each action, the system state is verified to ensure consistency across all components.

### 4. Error Recovery Testing
Simulates failures and verifies the system's ability to recover gracefully.

### 5. Performance Benchmarking
Measures response times and throughput under various load conditions.

## Running the Tests

### Prerequisites
- .NET 9 SDK
- Docker Desktop (for TestContainers)
- Sufficient system resources (8GB RAM minimum)

### Execution Commands

```bash
# Run all journey tests
dotnet test tests/SportsbookLite.JourneyTests

# Run specific test category
dotnet test --filter "Category=HappyPath"

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run with performance profiling
dotnet test --collect:"XPlat Code Coverage"
```

### CI/CD Integration

```yaml
name: Journey Tests
on: [push, pull_request]

jobs:
  journey-tests:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 9.0.x
    - name: Run Journey Tests
      run: dotnet test tests/SportsbookLite.JourneyTests --logger trx
    - name: Publish Test Results
      uses: dorny/test-reporter@v1
      if: always()
      with:
        name: Journey Test Results
        path: '**/*.trx'
        reporter: dotnet-trx
```

## Test Coverage Matrix

| Journey Type | Coverage | Priority | Automated |
|-------------|----------|----------|-----------|
| Happy Path | User registration to payout | High | ✅ |
| Admin Operations | Event lifecycle management | High | ✅ |
| Concurrent Betting | Multi-user scenarios | High | ✅ |
| Cashout Flows | Early settlement | Medium | ✅ |
| Error Recovery | Failure handling | High | ✅ |
| Performance | Load testing | Medium | ✅ |
| Edge Cases | Boundary conditions | Low | ⚠️ |

## Conclusion

The Journey Tests provide comprehensive validation of the Sportsbook-Lite application through realistic user scenarios. The Fluent Builder pattern ensures tests are readable, maintainable, and accurately reflect real-world usage patterns. The integration with TestContainers and Orleans TestCluster provides a realistic testing environment that closely mirrors production behavior.