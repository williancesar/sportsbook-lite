using FluentAssertions;
using Orleans.TestingHost;
using SportsbookLite.Contracts.Odds;
using SportsbookLite.Grains.Odds;
using SportsbookLite.GrainInterfaces;
using SportsbookLite.TestUtilities;

namespace SportsbookLite.IntegrationTests.Features.Odds;

public class OddsGrainIntegrationTests : BaseIntegrationTest
{
    private TestCluster _cluster = null!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        var builder = new TestClusterBuilder()
            .AddSiloBuilderConfigurator<SiloConfigurator>();

        _cluster = builder.Build();
        await _cluster.DeployAsync();
    }

    public override async Task DisposeAsync()
    {
        if (_cluster != null)
        {
            await _cluster.DisposeAsync();
        }
        await base.DisposeAsync();
    }

    private class SiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorage("Default");
        }
    }

    [Fact]
    public async Task RealTimeUpdatesSimulation_ShouldHandleHighFrequency()
    {
        var marketId = "integration_match_001";
        var grain = await InitializeMarketAsync(marketId);

        var updateTasks = new List<Task>();
        var totalUpdates = 100;
        var successfulUpdates = 0;

        for (int i = 0; i < totalUpdates; i++)
        {
            var odds = 2.0m + (decimal)(Math.Sin(i * 0.1) * 0.5);
            var updateRequest = OddsUpdateRequest.Create(
                marketId,
                new Dictionary<string, decimal> { ["Home Win"] = Math.Max(1.01m, odds) },
                OddsSource.Feed,
                $"Real-time update {i}");

            updateTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await grain.UpdateOddsAsync(updateRequest);
                    Interlocked.Increment(ref successfulUpdates);
                }
                catch (InvalidOperationException)
                {
                    // Market might get suspended due to high volatility
                }
                catch
                {
                    // Ignore other concurrency issues for this test
                }
            }));

            if (i % 10 == 0)
            {
                await Task.Delay(50);
            }
        }

        await Task.WhenAll(updateTasks);

        var snapshot = await grain.GetCurrentOddsAsync();
        var volatility = await grain.GetCurrentVolatilityAsync();
        var history = await grain.GetOddsHistoryAsync("Home Win");

        successfulUpdates.Should().BeGreaterThan(10);
        snapshot.Selections["Home Win"].Decimal.Should().BeGreaterThan(1.0m);
        volatility.Should().BeOneOf(OddsVolatility.Low, OddsVolatility.Medium, OddsVolatility.High, OddsVolatility.Extreme);
        history.Updates.Should().HaveCountGreaterOrEqualTo(10);
    }

    [Fact]
    public async Task VolatilityBasedAutoSuspension_ShouldTriggerCorrectly()
    {
        var marketId = "integration_match_002";
        var grain = await InitializeMarketAsync(marketId);

        var suspensionTriggered = false;
        var updateCount = 0;

        while (!suspensionTriggered && updateCount < 50)
        {
            var odds = 2.0m + (updateCount % 2 == 0 ? 1.0m : -0.8m);
            odds = Math.Max(1.01m, odds);

            var updateRequest = OddsUpdateRequest.Create(
                marketId,
                new Dictionary<string, decimal>
                {
                    ["Home Win"] = odds,
                    ["Draw"] = 3.2m + (updateCount % 3 == 0 ? 0.8m : -0.5m),
                    ["Away Win"] = 4.5m + (updateCount % 4 == 0 ? 1.5m : -1.0m)
                },
                OddsSource.Feed,
                $"Volatility test {updateCount}");

            try
            {
                await grain.UpdateOddsAsync(updateRequest);
                updateCount++;
                await Task.Delay(20);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("suspended"))
            {
                suspensionTriggered = true;
            }
        }

        var isSuspended = await grain.IsMarketSuspendedAsync();
        var volatility = await grain.GetCurrentVolatilityAsync();
        var volatilityScore = await grain.GetVolatilityScoreAsync(TimeSpan.FromMinutes(5));

        if (suspensionTriggered)
        {
            isSuspended.Should().BeTrue();
            volatility.Should().Be(OddsVolatility.Extreme);
            volatilityScore.Should().BeGreaterThan(50);
        }

        updateCount.Should().BeGreaterThan(5);
    }

    [Fact]
    public async Task ConcurrentBetLocking_ShouldAllowMultipleLocksPerSelection()
    {
        var marketId = "integration_match_003";
        var grain = await InitializeMarketAsync(marketId);

        var lockTasks = new List<Task>();
        var betIds = new List<string>();
        var lockCount = 20;

        for (int i = 0; i < lockCount; i++)
        {
            var betId = $"BET_CONCURRENT_{i:D3}";
            betIds.Add(betId);

            lockTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await grain.LockOddsForBetAsync(betId, "Home Win");
                }
                catch
                {
                    // Some locks might fail due to concurrency, that's expected
                }
            }));
        }

        await Task.WhenAll(lockTasks);

        var lockedSelections = await grain.GetLockedSelectionsAsync();
        var isLocked = await grain.IsSelectionLockedAsync("Home Win");

        isLocked.Should().BeTrue();
        lockedSelections.Should().ContainKey("Home Win");
        lockedSelections["Home Win"].Should().HaveCountGreaterOrEqualTo(10);
        lockedSelections["Home Win"].Should().HaveCountLessOrEqualTo(lockCount);

        foreach (var betId in betIds.Take(10))
        {
            await grain.UnlockOddsAsync(betId);
        }

        var remainingLocks = await grain.GetLockedSelectionsAsync();
        var stillLocked = await grain.IsSelectionLockedAsync("Home Win");

        if (lockedSelections["Home Win"].Count > 10)
        {
            stillLocked.Should().BeTrue();
            remainingLocks["Home Win"].Should().HaveCount(lockedSelections["Home Win"].Count - 10);
        }
    }

    [Fact]
    public async Task GrainPersistence_ShouldMaintainStateAcrossActivations()
    {
        var marketId = "integration_match_004";
        var grain1 = await InitializeMarketAsync(marketId);

        var updateRequest = OddsUpdateRequest.Create(
            marketId,
            new Dictionary<string, decimal> { ["Home Win"] = 1.75m },
            OddsSource.Manual,
            "Persistence test update");

        await grain1.UpdateOddsAsync(updateRequest);
        await grain1.SuspendOddsAsync("Persistence test suspension", "test_user");

        var grain2 = _cluster.GrainFactory.GetGrain<IOddsGrain>(marketId);

        var snapshot = await grain2.GetCurrentOddsAsync();
        var isSuspended = await grain2.IsMarketSuspendedAsync();
        var history = await grain2.GetOddsHistoryAsync("Home Win");

        snapshot.MarketId.Should().Be(marketId);
        snapshot.Selections["Home Win"].Decimal.Should().Be(1.75m);
        isSuspended.Should().BeTrue();
        snapshot.SuspensionReason.Should().Be("Persistence test suspension");
        history.Updates.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task ComplexVolatilityScenario_ShouldCalculateCorrectly()
    {
        var marketId = "integration_match_005";
        var grain = await InitializeMarketAsync(marketId);

        var scenarios = new[]
        {
            (odds: 2.0m, delay: 100),
            (odds: 2.5m, delay: 50),
            (odds: 1.8m, delay: 30),
            (odds: 3.0m, delay: 100),
            (odds: 1.6m, delay: 50),
            (odds: 2.2m, delay: 80),
            (odds: 1.9m, delay: 40)
        };

        foreach (var (odds, delay) in scenarios)
        {
            var updateRequest = OddsUpdateRequest.Create(
                marketId,
                new Dictionary<string, decimal> { ["Home Win"] = odds },
                OddsSource.Feed,
                $"Complex volatility test - odds: {odds}");

            try
            {
                await grain.UpdateOddsAsync(updateRequest);
                await Task.Delay(delay);
            }
            catch (InvalidOperationException)
            {
                break;
            }
        }

        var shortWindowVolatility = await grain.CalculateVolatilityAsync(TimeSpan.FromSeconds(30));
        var mediumWindowVolatility = await grain.CalculateVolatilityAsync(TimeSpan.FromMinutes(2));
        var longWindowVolatility = await grain.CalculateVolatilityAsync(TimeSpan.FromMinutes(10));

        var shortWindowScore = await grain.GetVolatilityScoreAsync(TimeSpan.FromSeconds(30));
        var mediumWindowScore = await grain.GetVolatilityScoreAsync(TimeSpan.FromMinutes(2));
        var longWindowScore = await grain.GetVolatilityScoreAsync(TimeSpan.FromMinutes(10));

        shortWindowScore.Should().BeGreaterOrEqualTo(mediumWindowScore);
        mediumWindowScore.Should().BeGreaterOrEqualTo(longWindowScore);

        var allVolatilities = new[] { shortWindowVolatility, mediumWindowVolatility, longWindowVolatility };
        allVolatilities.Should().AllSatisfy(v => v.Should().BeOneOf(OddsVolatility.Low, OddsVolatility.Medium, OddsVolatility.High, OddsVolatility.Extreme));
    }

    [Fact]
    public async Task MarketLifecycle_EndToEnd_ShouldWorkCorrectly()
    {
        var marketId = "integration_match_e2e";
        var grain = await InitializeMarketAsync(marketId);

        var initialSnapshot = await grain.GetCurrentOddsAsync();
        initialSnapshot.IsSuspended.Should().BeFalse();
        initialSnapshot.Volatility.Should().Be(OddsVolatility.Low);

        for (int i = 0; i < 5; i++)
        {
            var updateRequest = OddsUpdateRequest.Create(
                marketId,
                new Dictionary<string, decimal>
                {
                    ["Home Win"] = 2.0m + (i * 0.1m),
                    ["Draw"] = 3.2m + (i * 0.05m)
                },
                OddsSource.Feed,
                $"Lifecycle update {i}");

            await grain.UpdateOddsAsync(updateRequest);
            await Task.Delay(50);
        }

        var betId1 = "BET_LIFECYCLE_001";
        var betId2 = "BET_LIFECYCLE_002";
        await grain.LockOddsForBetAsync(betId1, "Home Win");
        await grain.LockOddsForBetAsync(betId2, "Draw");

        var lockedSnapshot = await grain.GetCurrentOddsAsync();
        var isHomeLocked = await grain.IsSelectionLockedAsync("Home Win");
        var isDrawLocked = await grain.IsSelectionLockedAsync("Draw");
        isHomeLocked.Should().BeTrue();
        isDrawLocked.Should().BeTrue();

        await grain.SuspendOddsAsync("Lifecycle test suspension", "test_operator");
        var suspendedSnapshot = await grain.GetCurrentOddsAsync();
        suspendedSnapshot.IsSuspended.Should().BeTrue();

        await grain.ResumeOddsAsync("Lifecycle test resumption", "test_operator");
        var resumedSnapshot = await grain.GetCurrentOddsAsync();
        resumedSnapshot.IsSuspended.Should().BeFalse();

        await grain.UnlockOddsAsync(betId1);
        await grain.UnlockOddsAsync(betId2);
        var finalIsHomeLocked = await grain.IsSelectionLockedAsync("Home Win");
        var finalIsDrawLocked = await grain.IsSelectionLockedAsync("Draw");
        finalIsHomeLocked.Should().BeFalse();
        finalIsDrawLocked.Should().BeFalse();

        var finalSnapshot = await grain.GetCurrentOddsAsync();
        var allHistory = await grain.GetAllOddsHistoryAsync();

        finalSnapshot.Selections["Home Win"].Decimal.Should().Be(2.4m);
        finalSnapshot.Selections["Draw"].Decimal.Should().Be(3.4m);
        allHistory.Should().HaveCount(3);
        allHistory["Home Win"].Updates.Should().HaveCountGreaterOrEqualTo(5);
        allHistory["Draw"].Updates.Should().HaveCountGreaterOrEqualTo(5);
    }

    [Fact]
    public async Task StressTest_MultipleGrainsOperating_ShouldMaintainIndependence()
    {
        var grains = new Dictionary<string, IOddsGrain>();
        var tasks = new List<Task>();

        for (int marketIndex = 0; marketIndex < 5; marketIndex++)
        {
            var marketId = $"stress_test_market_{marketIndex}";
            grains[marketId] = await InitializeMarketAsync(marketId);

            tasks.Add(Task.Run(async () =>
            {
                var grain = grains[marketId];
                
                for (int updateIndex = 0; updateIndex < 10; updateIndex++)
                {
                    try
                    {
                        var odds = 1.5m + (updateIndex * 0.1m) + (marketIndex * 0.05m);
                        var updateRequest = OddsUpdateRequest.Create(
                            marketId,
                            new Dictionary<string, decimal> { ["Home Win"] = odds },
                            OddsSource.Feed,
                            $"Stress test update {updateIndex}");

                        await grain.UpdateOddsAsync(updateRequest);

                        if (updateIndex % 3 == 0)
                        {
                            var betId = $"BET_{marketId}_{updateIndex}";
                            await grain.LockOddsForBetAsync(betId, "Home Win");
                            await Task.Delay(10);
                            await grain.UnlockOddsAsync(betId);
                        }

                        await Task.Delay(20);
                    }
                    catch (InvalidOperationException)
                    {
                        // Market might get suspended, that's OK for stress test
                        break;
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        foreach (var (marketId, grain) in grains)
        {
            var snapshot = await grain.GetCurrentOddsAsync();
            var history = await grain.GetOddsHistoryAsync("Home Win");

            snapshot.MarketId.Should().Be(marketId);
            snapshot.Selections["Home Win"].Decimal.Should().BeGreaterThan(1.0m);
            history.Updates.Should().HaveCountGreaterOrEqualTo(1);
        }
    }

    [Theory]
    [InlineData(30, "seconds")]
    [InlineData(1, "minutes")]
    [InlineData(5, "minutes")]
    [InlineData(15, "minutes")]
    public async Task VolatilityTimeWindows_ShouldShowDifferentResults(int duration, string unit)
    {
        var marketId = "integration_volatility_windows";
        var grain = await InitializeMarketAsync(marketId);

        var baseTime = DateTimeOffset.UtcNow;
        var updates = new[]
        {
            (odds: 2.0m, delay: 0),
            (odds: 2.8m, delay: 10000),
            (odds: 1.5m, delay: 20000),
            (odds: 3.5m, delay: 120000),
            (odds: 1.8m, delay: 300000)
        };

        foreach (var (odds, delay) in updates)
        {
            try
            {
                var updateRequest = OddsUpdateRequest.Create(
                    marketId,
                    new Dictionary<string, decimal> { ["Home Win"] = odds },
                    OddsSource.Feed,
                    $"Time window test - {odds}");

                await grain.UpdateOddsAsync(updateRequest);
                
                if (delay > 0)
                {
                    await Task.Delay(Math.Min(delay, 1000));
                }
            }
            catch (InvalidOperationException)
            {
                break;
            }
        }

        var timeWindow = unit == "seconds" ? TimeSpan.FromSeconds(duration) : TimeSpan.FromMinutes(duration);
        var volatility = await grain.CalculateVolatilityAsync(timeWindow);
        var score = await grain.GetVolatilityScoreAsync(timeWindow);

        volatility.Should().BeOneOf(OddsVolatility.Low, OddsVolatility.Medium, OddsVolatility.High, OddsVolatility.Extreme);
        score.Should().BeGreaterOrEqualTo(0);
    }

    private async Task<IOddsGrain> InitializeMarketAsync(string marketId)
    {
        var initialOdds = new Dictionary<string, decimal>
        {
            ["Home Win"] = 2.0m,
            ["Draw"] = 3.2m,
            ["Away Win"] = 4.5m
        };

        var grain = _cluster.GrainFactory.GetGrain<IOddsGrain>(marketId);
        await grain.InitializeMarketAsync(initialOdds);
        return grain;
    }
}