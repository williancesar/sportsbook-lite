# Contributing to SportsbookLite

First off, thank you for considering contributing to SportsbookLite! It's people like you that make SportsbookLite such a great distributed systems demonstration project.

## 📋 Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [How Can I Contribute?](#how-can-i-contribute)
- [Development Process](#development-process)
- [Coding Standards](#coding-standards)
- [Testing Requirements](#testing-requirements)
- [Pull Request Process](#pull-request-process)
- [Documentation Standards](#documentation-standards)
- [Community](#community)

## Code of Conduct

This project and everyone participating in it is governed by the [SportsbookLite Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to [conduct@sportsbook-lite.com](mailto:conduct@sportsbook-lite.com).

## Getting Started

### Prerequisites

Before you begin, ensure you have the following installed:

- .NET 9 SDK
- Docker Desktop 4.25+
- Your favorite IDE (Visual Studio 2022, VS Code, or JetBrains Rider)
- Git

### Setting Up Your Development Environment

1. **Fork the repository**
   ```bash
   # Click the 'Fork' button on GitHub, then clone your fork
   git clone https://github.com/yourusername/sportsbook-lite.git
   cd sportsbook-lite
   ```

2. **Add upstream remote**
   ```bash
   git remote add upstream https://github.com/original/sportsbook-lite.git
   git fetch upstream
   ```

3. **Create a feature branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

4. **Start development environment**
   ```bash
   # Start infrastructure services
   docker-compose -f docker/docker-compose.yml up -d

   # Run database migrations
   dotnet ef database update --project src/SportsbookLite.Infrastructure

   # Build the solution
   dotnet build

   # Run tests to verify setup
   dotnet test
   ```

## How Can I Contribute?

### 🐛 Reporting Bugs

Before creating bug reports, please check existing issues as you might find that you don't need to create one. When you are creating a bug report, please include as many details as possible:

**Bug Report Template:**
```markdown
**Describe the bug**
A clear and concise description of what the bug is.

**To Reproduce**
Steps to reproduce the behavior:
1. Start Orleans silo with '...'
2. Send API request to '....'
3. Check grain state '....'
4. See error

**Expected behavior**
What you expected to happen.

**Actual behavior**
What actually happened.

**Environment:**
- OS: [e.g., Windows 11, Ubuntu 22.04]
- .NET Version: [e.g., 9.0.100]
- Orleans Version: [e.g., 9.2.1]
- Docker Version: [e.g., 24.0.7]

**Logs**
```
Paste relevant logs here
```

**Additional context**
Add any other context about the problem here.
```

### 💡 Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion, please include:

- **Use a clear and descriptive title**
- **Provide a step-by-step description** of the suggested enhancement
- **Provide specific examples** to demonstrate the steps
- **Describe the current behavior** and explain which behavior you expected to see instead
- **Explain why this enhancement would be useful** to most users
- **List some other projects** where this enhancement exists (if applicable)

### 🔧 Your First Code Contribution

Unsure where to begin? You can start by looking through these issues:

- Issues labeled `good first issue` - issues which should only require a few lines of code
- Issues labeled `help wanted` - issues which need extra attention
- Issues labeled `documentation` - improvements or additions to documentation

## Development Process

### 🌿 Git Workflow

We use GitHub Flow - it's simple and effective:

1. **Create a feature branch** from `main`
2. **Make your changes** in logical commits
3. **Write/update tests** as needed
4. **Update documentation** if needed
5. **Submit a pull request** to `main`

### 📝 Commit Message Guidelines

We follow conventional commits for clear history:

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only changes
- `style`: Code style changes (formatting, missing semicolons, etc.)
- `refactor`: Code change that neither fixes a bug nor adds a feature
- `perf`: Performance improvement
- `test`: Adding or correcting tests
- `chore`: Changes to build process or auxiliary tools
- `ci`: CI/CD related changes

**Examples:**
```bash
feat(grains): add idempotency to BetGrain operations

fix(api): handle null reference in odds validation

docs(readme): update Orleans version in prerequisites

perf(wallet): optimize balance calculation with caching

test(integration): add Pulsar consumer retry scenarios
```

## Coding Standards

### 🔤 C# / .NET Standards

Follow Microsoft's [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) with these specific requirements:

#### Orleans Grain Standards

```csharp
// ✅ GOOD - Proper grain interface
public interface IBetGrain : IGrainWithGuidKey
{
    ValueTask<BetResult> PlaceBetAsync(PlaceBetRequest request);
    ValueTask<BetState> GetStateAsync();
}

// ✅ GOOD - Proper grain implementation
[Alias("bet")]
public sealed class BetGrain : Grain, IBetGrain
{
    private readonly IPersistentState<BetState> _state;

    public BetGrain(
        [PersistentState("bet", "BettingStore")]
        IPersistentState<BetState> state)
    {
        _state = state;
    }

    public async ValueTask<BetResult> PlaceBetAsync(PlaceBetRequest request)
    {
        // Validate
        ArgumentNullException.ThrowIfNull(request);

        // Process
        _state.State.Amount = request.Amount;
        _state.State.Status = BetStatus.Pending;

        // Persist
        await _state.WriteStateAsync();

        // Return result
        return new BetResult { Success = true };
    }
}

// ❌ BAD - Don't use Task instead of ValueTask
public Task<BetResult> PlaceBetAsync(PlaceBetRequest request) // Wrong!

// ❌ BAD - Don't block async calls
var result = PlaceBetAsync(request).Result; // Never do this!
```

#### Async/Await Best Practices

```csharp
// ✅ GOOD - Parallel execution
public async ValueTask<DashboardData> GetDashboardAsync()
{
    var (bets, balance, odds) = await (
        GetRecentBetsAsync(),
        GetBalanceAsync(),
        GetCurrentOddsAsync()
    );

    return new DashboardData(bets, balance, odds);
}

// ❌ BAD - Sequential when parallel is possible
var bets = await GetRecentBetsAsync();
var balance = await GetBalanceAsync();  // Could run in parallel!
var odds = await GetCurrentOddsAsync();  // Could run in parallel!
```

#### Dependency Injection

```csharp
// ✅ GOOD - Constructor injection
public sealed class BettingService
{
    private readonly IGrainFactory _grainFactory;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<BettingService> _logger;

    public BettingService(
        IGrainFactory grainFactory,
        IEventPublisher eventPublisher,
        ILogger<BettingService> logger)
    {
        _grainFactory = grainFactory;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }
}
```

### 🏗️ Project Structure Standards

Maintain the vertical slice architecture:

```
Features/
├── Betting/                  # Feature slice
│   ├── Endpoints/            # FastEndpoints
│   ├── Grains/              # Orleans grains
│   ├── Events/              # Domain events
│   ├── Handlers/            # Event handlers
│   ├── Models/              # Domain models
│   └── Validators/          # FluentValidation
```

### 📦 NuGet Package Standards

- Only add packages that provide significant value
- Prefer Microsoft-maintained packages when available
- Document why each package is needed
- Keep packages updated to latest stable versions

## Testing Requirements

### ✅ Test Coverage Requirements

- **Minimum coverage**: 80% for new code
- **Critical paths**: 95% coverage (betting, wallet operations)
- **All public APIs** must have integration tests
- **All grains** must have unit tests

### 🧪 Test Structure

#### Unit Tests

```csharp
public class BetGrainTests : IAsyncLifetime
{
    private TestCluster _cluster = null!;

    public async Task InitializeAsync()
    {
        var builder = new TestClusterBuilder();
        _cluster = builder.Build();
        await _cluster.DeployAsync();
    }

    [Fact]
    public async Task PlaceBet_ValidRequest_ShouldSucceed()
    {
        // Arrange
        var grain = _cluster.GrainFactory.GetGrain<IBetGrain>(Guid.NewGuid());
        var request = new PlaceBetRequest
        {
            Amount = 100m,
            UserId = "user123",
            MarketId = "market456"
        };

        // Act
        var result = await grain.PlaceBetAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.BetId.Should().NotBeEmpty();
    }

    public Task DisposeAsync()
    {
        return _cluster.DisposeAsync().AsTask();
    }
}
```

#### Integration Tests

```csharp
public class BettingApiTests : IntegrationTestBase
{
    [Fact]
    public async Task PlaceBet_EndToEnd_ShouldWork()
    {
        // Arrange
        var client = Factory.CreateClient();
        var request = new PlaceBetRequest { /* ... */ };

        // Act
        var response = await client.PostAsJsonAsync("/api/bets", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<BetResult>();
        result.Should().NotBeNull();

        // Verify event published to Pulsar
        var events = await ConsumeEvents<BetPlacedEvent>(1);
        events.Should().ContainSingle();
    }
}
```

### 🏃 Running Tests

```bash
# Run all tests
dotnet test

# Run specific category
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
dotnet test --filter Category=Journey

# Run with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Generate coverage report
reportgenerator -reports:coverage/**/coverage.cobertura.xml -targetdir:coverage/report
```

## Pull Request Process

### 📋 PR Checklist

Before submitting your PR, ensure:

- [ ] Code compiles without warnings
- [ ] All tests pass locally
- [ ] Test coverage meets requirements (80%+)
- [ ] Documentation is updated (if needed)
- [ ] Commit messages follow our convention
- [ ] PR description clearly describes changes
- [ ] Breaking changes are documented
- [ ] Performance impact is considered

### 🔄 PR Template

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

## Testing
- [ ] Unit tests pass
- [ ] Integration tests pass
- [ ] Journey tests pass (if applicable)
- [ ] Manual testing completed

## Checklist
- [ ] My code follows the style guidelines of this project
- [ ] I have performed a self-review of my own code
- [ ] I have commented my code, particularly in hard-to-understand areas
- [ ] I have made corresponding changes to the documentation
- [ ] My changes generate no new warnings
- [ ] I have added tests that prove my fix is effective or that my feature works
- [ ] New and existing unit tests pass locally with my changes

## Performance Impact
Describe any performance implications

## Screenshots (if appropriate)
Add screenshots for UI changes
```

### 👀 Code Review Process

1. **Automated checks** must pass (CI/CD pipeline)
2. **At least one maintainer** must review
3. **All feedback** must be addressed
4. **Final approval** from a maintainer
5. **Squash and merge** to maintain clean history

### 🚀 After Your PR is Merged

- Delete your feature branch
- Pull the latest `main` to your local repository
- Celebrate your contribution! 🎉

## Documentation Standards

### 📚 Code Documentation

```csharp
/// <summary>
/// Places a new bet for a user on a specific market.
/// </summary>
/// <param name="request">The bet placement request containing amount, selection, and odds.</param>
/// <returns>A result indicating success or failure with bet details.</returns>
/// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
/// <exception cref="InsufficientFundsException">Thrown when user has insufficient balance.</exception>
public async ValueTask<BetResult> PlaceBetAsync(PlaceBetRequest request)
{
    // Implementation
}
```

### 📖 README Updates

When adding new features, update relevant sections:
- API Documentation (if new endpoints)
- Configuration (if new settings)
- Development Setup (if new dependencies)

## Community

### 💬 Communication Channels

- **GitHub Discussions**: For general questions and discussions
- **GitHub Issues**: For bug reports and feature requests
- **Discord**: [Join our Discord](https://discord.gg/sportsbook-lite) for real-time chat
- **Twitter**: Follow [@SportsbookLite](https://twitter.com/sportsbooklite) for updates

### 🏆 Recognition

Contributors who make significant contributions will be:
- Added to the [CONTRIBUTORS.md](CONTRIBUTORS.md) file
- Mentioned in release notes
- Given credit in documentation

### 📈 Becoming a Maintainer

Active contributors who demonstrate:
- Consistent high-quality contributions
- Good understanding of the codebase
- Helpful participation in issues and PRs
- Alignment with project goals

May be invited to become maintainers with additional permissions.

## 📮 Questions?

Don't hesitate to ask questions! We're here to help:

- Open a [GitHub Discussion](https://github.com/yourusername/sportsbook-lite/discussions)
- Reach out on [Discord](https://discord.gg/sportsbook-lite)
- Email: [contributors@sportsbook-lite.com](mailto:contributors@sportsbook-lite.com)

---

**Thank you for contributing to SportsbookLite! Together, we're building an excellent example of distributed systems with Orleans and event-driven architecture.** 🚀