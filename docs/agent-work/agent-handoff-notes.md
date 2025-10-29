# Agent Handoff Notes

## Overview
This document facilitates smooth transitions between specialized agents during the implementation of Sportsbook-Lite. Each section provides context and specific instructions for the next agent.

## Current Status
**Date**: 2025-09-11  
**Current Phase**: Phase 2 - User Wallet Vertical Slice  
**Last Agent**: test-automator (completed Phase 1.4)  
**Next Agent**: csharp-pro (Phase 2.1-2.2)  

## Active Context

### Repository State
- All foundation projects created and building successfully
- 9 projects total: 6 source, 3 test projects
- Orleans, FastEndpoints, and infrastructure packages configured
- Test infrastructure ready with TestContainers and Orleans TestCluster
- Solution builds with 4 minor warnings (async methods)

### Environment
- Working Directory: `/home/willian/Repos/sportsbook-lite`
- Platform: Linux
- .NET SDK: Should verify .NET 9 is installed
- Git Repository: Initialized, on main branch

## Phase 1 Handoff Instructions

### For csharp-pro Agent (Phase 1.1 - 1.3)

**Your Tasks**:
1. Create all .NET 9 projects according to the structure in CLAUDE.md
2. Setup proper project references
3. Configure Orleans and FastEndpoints
4. Ensure all projects target .NET 9

**Projects to Create**:
```
src/
├── SportsbookLite.Contracts           (Class Library)
├── SportsbookLite.GrainInterfaces     (Class Library)
├── SportsbookLite.Infrastructure      (Class Library)
├── SportsbookLite.Grains              (Class Library)
├── SportsbookLite.Host                (Console Application)
├── SportsbookLite.Api                 (Web API)
└── Features/                           (Folder for vertical slices)
    ├── Betting/
    ├── Events/
    ├── Odds/
    └── Wallet/
```

**Key Requirements**:
- Use .NET 9 and C# 13 features
- Enable nullable reference types
- Configure global usings appropriately
- Setup Orleans packages in relevant projects
- Configure FastEndpoints in API project
- Follow naming conventions from CLAUDE.md

**Dependencies to Add**:
- Orleans: Microsoft.Orleans.Server, Microsoft.Orleans.Client
- FastEndpoints: FastEndpoints, FastEndpoints.Swagger
- Infrastructure: Pulsar.Client, Npgsql, StackExchange.Redis
- Testing: xUnit, FluentAssertions, NSubstitute
- Logging: Serilog.AspNetCore, Serilog.Sinks.Console
- Observability: OpenTelemetry packages

**Project References**:
```
SportsbookLite.Host → Grains, GrainInterfaces, Infrastructure
SportsbookLite.Api → GrainInterfaces, Contracts, Infrastructure
SportsbookLite.Grains → GrainInterfaces, Contracts, Infrastructure
SportsbookLite.Infrastructure → Contracts
Features/* → Grains, GrainInterfaces, Contracts, Infrastructure
```

### For test-automator Agent (Phase 1.4)

**Your Tasks**:
1. Create test projects with proper structure
2. Setup TestContainers for integration testing
3. Configure Orleans TestCluster
4. Create base test classes

**Test Projects to Create**:
```
tests/
├── SportsbookLite.UnitTests
├── SportsbookLite.IntegrationTests
└── SportsbookLite.TestUtilities
```

**Key Requirements**:
- Configure xUnit with proper test collection settings
- Setup TestContainers for PostgreSQL and Pulsar
- Create TestCluster configuration for Orleans
- Implement base classes for common test scenarios
- Configure code coverage settings

### For backend-architect Agent (Phase 2-6 API Design)

**Context**: After csharp-pro creates the base projects, you'll design and implement API endpoints using FastEndpoints for each vertical slice.

**Key Considerations**:
- Follow RESTful principles
- Implement proper validation
- Use FastEndpoints request/response pattern
- Configure OpenAPI documentation
- Implement versioning strategy
- Follow vertical slice architecture

**API Structure Pattern**:
```csharp
public class PlaceBetEndpoint : Endpoint<PlaceBetRequest, PlaceBetResponse>
{
    public override void Configure()
    {
        Post("/api/bets");
        Version(1);
        Summary(s => { /* OpenAPI docs */ });
    }
}
```

### For deployment-engineer Agent (Phase 7)

**Context**: After core functionality is complete, you'll containerize and setup deployment.

**Key Tasks**:
- Multi-stage Dockerfile
- Docker Compose for local development
- Kubernetes manifests
- CI/CD pipeline with GitHub Actions

**Important Files**:
- `docker/Dockerfile`
- `docker/docker-compose.yml`
- `k8s/*.yaml`
- `.github/workflows/ci.yml`

## Vertical Slice Implementation Pattern

Each vertical slice should follow this structure:

```
Features/[SliceName]/
├── Endpoints/          # FastEndpoints API
├── Grains/            # Orleans grain implementations
├── Events/            # Domain events
├── Handlers/          # Event handlers
├── Models/            # Domain models
├── Validators/        # Input validation
└── Tests/             # Slice-specific tests
```

## Critical Success Factors

1. **Orleans Best Practices**:
   - Always use `ValueTask` for grain methods
   - Keep grain state serializable and small
   - Use grain aliases for persistence
   - Implement proper activation/deactivation

2. **Event-Driven Architecture**:
   - Events are immutable records
   - Use correlation IDs
   - Handle out-of-order events
   - Implement idempotency

3. **Code Quality**:
   - No comments unless necessary
   - Self-documenting code
   - Follow SOLID principles
   - Comprehensive test coverage

4. **Performance**:
   - Use parallel execution where possible
   - Implement caching strategically
   - Batch operations when feasible
   - Use appropriate grain placement

## Common Pitfalls to Avoid

1. Never use `.Result` or `.Wait()` on tasks
2. Don't create circular project references
3. Avoid grain-to-grain communication loops
4. Don't store large objects in grain state
5. Never commit secrets or connection strings

## Verification Checklist

After each phase, verify:
- [ ] Solution builds without warnings
- [ ] All tests pass
- [ ] No TODO comments remain
- [ ] Code follows conventions from CLAUDE.md
- [ ] Update implementation-progress.md
- [ ] Prepare handoff notes for next agent

## Questions/Blockers Log

*Record any questions or blockers here for the next agent*

- None yet

## Phase 2 Handoff Instructions (User Wallet Vertical Slice)

### For csharp-pro Agent (Phase 2.1-2.2)

**Your Tasks**:
1. Create wallet domain models and contracts in SportsbookLite.Contracts
2. Define transaction types and wallet events
3. Create IUserWalletGrain interface in SportsbookLite.GrainInterfaces
4. Implement UserWalletGrain in SportsbookLite.Grains with double-entry bookkeeping
5. Create WalletState for grain persistence

**Key Requirements**:
- Implement double-entry bookkeeping pattern for financial integrity
- Support operations: Deposit, Withdraw, GetBalance, GetTransactionHistory
- Each transaction should create debit and credit entries
- Implement idempotency using transaction IDs
- Use ValueTask for all grain methods
- Add proper validation (no negative balances, valid amounts)
- Create domain events: WalletCredited, WalletDebited, TransactionFailed

**Models to Create**:
```csharp
// Contracts
public record WalletTransaction
public record TransactionEntry (debit/credit)
public enum TransactionType { Deposit, Withdrawal, BetPlacement, BetWin }
public record Money (decimal Amount, string Currency)

// Events
public record WalletCreditedEvent : IDomainEvent
public record WalletDebitedEvent : IDomainEvent

// Grain Interface
public interface IUserWalletGrain : IGrainWithStringKey
{
    ValueTask<Money> GetBalanceAsync();
    ValueTask<TransactionResult> DepositAsync(Money amount, string transactionId);
    ValueTask<TransactionResult> WithdrawAsync(Money amount, string transactionId);
    ValueTask<bool> ReserveAsync(Money amount, string betId);
    ValueTask<bool> CommitReservationAsync(string betId);
    ValueTask<bool> ReleaseReservationAsync(string betId);
    ValueTask<IReadOnlyList<WalletTransaction>> GetTransactionHistoryAsync(int limit);
}
```

### For backend-architect Agent (Phase 2.3)

**Your Tasks**:
1. Create wallet API endpoints using FastEndpoints
2. Implement proper request/response DTOs
3. Add validation rules
4. Configure OpenAPI documentation

**Endpoints to Create**:
- POST /api/wallet/deposit
- POST /api/wallet/withdraw
- GET /api/wallet/balance
- GET /api/wallet/transactions

### For test-automator Agent (Phase 2.4)

**Your Tasks**:
1. Create comprehensive unit tests for wallet grain
2. Test double-entry bookkeeping logic
3. Test idempotency of operations
4. Create integration tests with TestCluster
5. Test API endpoints

## Last Updated
2025-09-11 - Phase 1 completed, ready for Phase 2 implementation