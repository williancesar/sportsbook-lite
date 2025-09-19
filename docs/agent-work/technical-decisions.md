# Technical Decisions Log

## Overview
This document captures key technical decisions made during the implementation of Sportsbook-Lite, providing rationale and context for future reference.

## Architecture Decisions

### AD-001: Vertical Slices Architecture
**Date**: 2025-09-11  
**Decision**: Organize code by features (vertical slices) rather than technical layers  
**Rationale**: 
- Improves feature cohesion and reduces cross-feature dependencies
- Aligns with domain-driven design principles
- Makes the codebase more maintainable and testable
- Each slice can be developed and deployed independently

### AD-002: Orleans Virtual Actor Model
**Date**: 2025-09-11  
**Decision**: Use Microsoft Orleans for distributed state management  
**Rationale**:
- Simplifies distributed systems complexity
- Provides automatic scaling and fault tolerance
- Virtual actors handle concurrency automatically
- Perfect for betting domain with user sessions and bet management

### AD-003: Apache Pulsar for Event Streaming
**Date**: 2025-09-11  
**Decision**: Use Apache Pulsar instead of Kafka or RabbitMQ  
**Rationale**:
- Multi-tenancy support out of the box
- Geo-replication capabilities for future scaling
- Better handling of large backlogs
- Unified messaging and streaming platform

### AD-004: FastEndpoints over Minimal APIs
**Date**: 2025-09-11  
**Decision**: Use FastEndpoints for API layer  
**Rationale**:
- Better performance than traditional controllers
- Built-in validation and documentation
- Cleaner separation of concerns
- Native support for vertical slice architecture

## Technology Stack Decisions

### TS-001: .NET 9 and C# 13
**Date**: 2025-09-11  
**Decision**: Use latest .NET 9 with C# 13 features  
**Rationale**:
- Demonstrates knowledge of latest technologies
- Performance improvements in .NET 9
- New C# 13 features improve code clarity
- Shows commitment to modern development

### TS-002: PostgreSQL for Persistence
**Date**: 2025-09-11  
**Decision**: PostgreSQL as primary database  
**Rationale**:
- Excellent performance for OLTP workloads
- Strong consistency guarantees
- Rich feature set (JSONB, arrays, etc.)
- Great Orleans grain storage provider support

### TS-003: Redis for Orleans Clustering
**Date**: 2025-09-11  
**Decision**: Redis for Orleans membership and clustering  
**Rationale**:
- Low latency for cluster operations
- Proven reliability in production
- Simple to deploy and manage
- Can double as cache layer

### TS-004: xUnit for Testing
**Date**: 2025-09-11  
**Decision**: xUnit as testing framework  
**Rationale**:
- Better async test support
- More extensible than NUnit
- Better integration with .NET tooling
- Industry standard for .NET projects

## Design Pattern Decisions

### DP-001: Event Sourcing for Bets
**Date**: 2025-09-11  
**Decision**: Implement event sourcing for bet aggregates  
**Rationale**:
- Natural audit trail for regulatory compliance
- Ability to replay events for debugging
- Temporal queries for bet history
- Supports event-driven architecture

### DP-002: CQRS for Read/Write Separation
**Date**: 2025-09-11  
**Decision**: Separate read and write models  
**Rationale**:
- Optimized read models for queries
- Write models focus on business logic
- Better scalability for read-heavy operations
- Aligns with Orleans grain pattern

### DP-003: Saga Pattern for Distributed Transactions
**Date**: 2025-09-11  
**Decision**: Use Saga pattern for multi-step operations  
**Rationale**:
- Handles distributed transactions without 2PC
- Built-in compensation for failures
- Better resilience in distributed systems
- Natural fit with event-driven architecture

### DP-004: Double-Entry Bookkeeping for Wallet
**Date**: 2025-09-11  
**Decision**: Implement double-entry bookkeeping for financial transactions  
**Rationale**:
- Ensures financial consistency
- Natural audit trail
- Prevents balance discrepancies
- Industry standard for financial systems

## Implementation Decisions

### ID-001: ValueTask for Grain Methods
**Date**: 2025-09-11  
**Decision**: Use ValueTask instead of Task for grain methods  
**Rationale**:
- Better performance for synchronous completions
- Reduced allocations
- Orleans best practice
- Aligns with high-performance goals

### ID-002: Grain Aliases for Persistence
**Date**: 2025-09-11  
**Decision**: Use [Alias] attribute for grain persistence  
**Rationale**:
- Decouples storage from class names
- Enables grain versioning
- Simplifies database migrations
- Better for long-term maintenance

### ID-003: Idempotent Operations
**Date**: 2025-09-11  
**Decision**: Make all critical operations idempotent  
**Rationale**:
- Handles network retries gracefully
- Prevents duplicate bets
- Essential for distributed systems
- Improves system reliability

### ID-004: Circuit Breaker for External Services
**Date**: 2025-09-11  
**Decision**: Implement circuit breaker pattern  
**Rationale**:
- Prevents cascade failures
- Quick failure detection
- Automatic recovery
- Better user experience during outages

## Security Decisions

### SC-001: API Key Authentication for Demo
**Date**: 2025-09-11  
**Decision**: Simple API key authentication for interview demo  
**Rationale**:
- Demonstrates authentication awareness
- Simple to implement for demo
- Can be extended to JWT/OAuth2
- Focus on core functionality for interview

### SC-002: Input Validation at API Layer
**Date**: 2025-09-11  
**Decision**: Validate all inputs at API boundary  
**Rationale**:
- Fail fast principle
- Better error messages for clients
- Reduces load on grains
- Security best practice

## DevOps Decisions

### DO-001: Multi-Stage Docker Builds
**Date**: 2025-09-11  
**Decision**: Use multi-stage Dockerfile  
**Rationale**:
- Smaller production images
- Better layer caching
- Separates build and runtime dependencies
- Industry best practice

### DO-002: GitHub Actions for CI/CD
**Date**: 2025-09-11  
**Decision**: GitHub Actions for automation  
**Rationale**:
- Native GitHub integration
- Free for public repositories
- Simple YAML configuration
- Wide marketplace of actions

### DO-003: Kubernetes StatefulSet for Orleans
**Date**: 2025-09-11  
**Decision**: Deploy Orleans silos as StatefulSet  
**Rationale**:
- Stable network identities
- Ordered deployment/scaling
- Persistent storage support
- Orleans clustering requirements

## Performance Decisions

### PF-001: Grain Placement Strategies
**Date**: 2025-09-11  
**Decision**: Use strategic grain placement  
**Rationale**:
- Reduces network hops
- Better cache locality
- Improved latency
- Orleans optimization best practice

### PF-002: Batch Processing for Settlement
**Date**: 2025-09-11  
**Decision**: Batch bet settlements  
**Rationale**:
- Reduces database round trips
- Better throughput
- Efficient resource usage
- Handles high-volume events

## Future Considerations
- Implement rate limiting for API endpoints
- Add distributed caching layer
- Implement blue-green deployments
- Add comprehensive monitoring and alerting
- Consider GraphQL for flexible queries
- Implement WebSockets for real-time odds updates