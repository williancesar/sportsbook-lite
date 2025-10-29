using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.TestingHost;
using SportsbookLite.Api;
using SportsbookLite.Api.Features.Wallet.Requests;
using SportsbookLite.Api.Features.Wallet.Responses;
using SportsbookLite.Contracts.Wallet;
using SportsbookLite.GrainInterfaces;
using SportsbookLite.Grains.Wallet;
using SportsbookLite.TestUtilities;
using System.Net;
using System.Net.Http.Json;

namespace SportsbookLite.IntegrationTests.Features.Wallet;

public class WalletApiTests : BaseIntegrationTest
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private TestCluster _cluster = null!;

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        var builder = new TestClusterBuilder()
            .AddSiloBuilderConfigurator<SiloConfigurator>();

        _cluster = builder.Build();
        await _cluster.DeployAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(hostBuilder =>
            {
                hostBuilder.UseConfiguration(Configuration);
                hostBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<IGrainFactory>(_cluster.GrainFactory);
                });
            });

        _client = _factory.CreateClient();
    }

    public override async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
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
    public async Task DepositEndpoint_WithValidRequest_ShouldReturnSuccess()
    {
        var userId = "api_user_001";
        var request = new DepositRequest
        {
            UserId = userId,
            Amount = 100.50m,
            Currency = "USD",
            TransactionId = Guid.NewGuid().ToString(),
            Description = "Test deposit"
        };

        var response = await _client.PostAsJsonAsync($"/api/wallet/{userId}/deposit", request);
        var result = await response.Content.ReadFromJsonAsync<DepositResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Transaction.Should().NotBeNull();
        result.Transaction!.Amount.Should().Be(100.50m);
        result.Transaction.Currency.Should().Be("USD");
        result.Transaction.Type.Should().Be("Deposit");
        result.Transaction.Status.Should().Be("Completed");
        result.NewBalance.Should().NotBeNull();
        result.NewBalance!.Amount.Should().Be(100.50m);
        result.NewBalance.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task DepositEndpoint_WithInvalidAmount_ShouldReturnBadRequest()
    {
        var userId = "api_user_002";
        var request = new DepositRequest
        {
            UserId = userId,
            Amount = 0m,
            Currency = "USD",
            TransactionId = Guid.NewGuid().ToString()
        };

        var response = await _client.PostAsJsonAsync($"/api/wallet/{userId}/deposit", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DepositEndpoint_WithNegativeAmount_ShouldReturnBadRequest()
    {
        var userId = "api_user_003";
        var request = new DepositRequest
        {
            UserId = userId,
            Amount = -50m,
            Currency = "USD",
            TransactionId = Guid.NewGuid().ToString()
        };

        var response = await _client.PostAsJsonAsync($"/api/wallet/{userId}/deposit", request);
        var result = await response.Content.ReadFromJsonAsync<DepositResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Amount cannot be negative");
    }

    [Fact]
    public async Task WithdrawEndpoint_WithSufficientFunds_ShouldReturnSuccess()
    {
        var userId = "api_user_004";
        var depositTransactionId = Guid.NewGuid().ToString();
        var withdrawTransactionId = Guid.NewGuid().ToString();

        var depositRequest = new DepositRequest
        {
            UserId = userId,
            Amount = 200m,
            Currency = "USD",
            TransactionId = depositTransactionId
        };
        await _client.PostAsJsonAsync($"/api/wallet/{userId}/deposit", depositRequest);

        var withdrawRequest = new WithdrawRequest
        {
            UserId = userId,
            Amount = 75m,
            Currency = "USD",
            TransactionId = withdrawTransactionId
        };

        var response = await _client.PostAsJsonAsync($"/api/wallet/{userId}/withdraw", withdrawRequest);
        var result = await response.Content.ReadFromJsonAsync<WithdrawResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Transaction.Should().NotBeNull();
        result.Transaction!.Amount.Should().Be(75m);
        result.Transaction.Type.Should().Be("Withdrawal");
        result.Transaction.Status.Should().Be("Completed");
        result.NewBalance.Should().NotBeNull();
        result.NewBalance!.Amount.Should().Be(125m);
    }

    [Fact]
    public async Task WithdrawEndpoint_WithInsufficientFunds_ShouldReturnBadRequest()
    {
        var userId = "api_user_005";
        var request = new WithdrawRequest
        {
            UserId = userId,
            Amount = 100m,
            Currency = "USD",
            TransactionId = Guid.NewGuid().ToString()
        };

        var response = await _client.PostAsJsonAsync($"/api/wallet/{userId}/withdraw", request);
        var result = await response.Content.ReadFromJsonAsync<WithdrawResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Insufficient available balance");
    }

    [Fact]
    public async Task GetBalanceEndpoint_ShouldReturnCurrentBalance()
    {
        var userId = "api_user_006";
        var depositTransactionId = Guid.NewGuid().ToString();

        var depositRequest = new DepositRequest
        {
            UserId = userId,
            Amount = 150.75m,
            Currency = "USD",
            TransactionId = depositTransactionId
        };
        await _client.PostAsJsonAsync($"/api/wallet/{userId}/deposit", depositRequest);

        var response = await _client.GetAsync($"/api/wallet/{userId}/balance");
        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Amount.Should().Be(150.75m);
        result.Currency.Should().Be("USD");
        result.AvailableAmount.Should().Be(150.75m);
    }

    [Fact]
    public async Task GetTransactionsEndpoint_ShouldReturnTransactionHistory()
    {
        var userId = "api_user_007";
        var depositTransactionId = Guid.NewGuid().ToString();
        var withdrawTransactionId = Guid.NewGuid().ToString();

        var depositRequest = new DepositRequest
        {
            UserId = userId,
            Amount = 300m,
            Currency = "USD",
            TransactionId = depositTransactionId
        };
        await _client.PostAsJsonAsync($"/api/wallet/{userId}/deposit", depositRequest);

        await Task.Delay(50);

        var withdrawRequest = new WithdrawRequest
        {
            UserId = userId,
            Amount = 100m,
            Currency = "USD",
            TransactionId = withdrawTransactionId
        };
        await _client.PostAsJsonAsync($"/api/wallet/{userId}/withdraw", withdrawRequest);

        var response = await _client.GetAsync($"/api/wallet/{userId}/transactions?limit=10");
        var result = await response.Content.ReadFromJsonAsync<TransactionsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Transactions.Should().NotBeNullOrEmpty();
        result.Transactions.Should().HaveCount(2);
        result.Transactions[0].Type.Should().Be("Withdrawal");
        result.Transactions[1].Type.Should().Be("Deposit");
        result.Transactions.All(t => t.Status == "Completed").Should().BeTrue();
    }

    [Fact]
    public async Task GetTransactionsEndpoint_WithLimit_ShouldRespectLimit()
    {
        var userId = "api_user_008";

        for (int i = 0; i < 5; i++)
        {
            var request = new DepositRequest
            {
                UserId = userId,
                Amount = 10m + i,
                Currency = "USD",
                TransactionId = Guid.NewGuid().ToString()
            };
            await _client.PostAsJsonAsync($"/api/wallet/{userId}/deposit", request);
            await Task.Delay(10);
        }

        var response = await _client.GetAsync($"/api/wallet/{userId}/transactions?limit=3");
        var result = await response.Content.ReadFromJsonAsync<TransactionsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Transactions.Should().HaveCount(3);
    }

    [Fact]
    public async Task DepositEndpoint_WithDuplicateTransactionId_ShouldBeIdempotent()
    {
        var userId = "api_user_009";
        var transactionId = Guid.NewGuid().ToString();
        var request = new DepositRequest
        {
            UserId = userId,
            Amount = 50.25m,
            Currency = "USD",
            TransactionId = transactionId
        };

        var firstResponse = await _client.PostAsJsonAsync($"/api/wallet/{userId}/deposit", request);
        var firstResult = await firstResponse.Content.ReadFromJsonAsync<DepositResponse>();

        var secondResponse = await _client.PostAsJsonAsync($"/api/wallet/{userId}/deposit", request);
        var secondResult = await secondResponse.Content.ReadFromJsonAsync<DepositResponse>();

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        firstResult!.IsSuccess.Should().BeTrue();
        secondResult!.IsSuccess.Should().BeTrue();
        
        firstResult.Transaction!.Id.Should().Be(secondResult.Transaction!.Id);
        firstResult.NewBalance!.Amount.Should().Be(secondResult.NewBalance!.Amount);
    }

    [Fact]
    public async Task WalletOperations_EndToEnd_ShouldMaintainConsistency()
    {
        var userId = "api_user_010";
        
        var deposit1Request = new DepositRequest
        {
            UserId = userId,
            Amount = 1000m,
            Currency = "USD",
            TransactionId = Guid.NewGuid().ToString()
        };
        await _client.PostAsJsonAsync($"/api/wallet/{userId}/deposit", deposit1Request);

        var deposit2Request = new DepositRequest
        {
            UserId = userId,
            Amount = 500m,
            Currency = "USD",
            TransactionId = Guid.NewGuid().ToString()
        };
        await _client.PostAsJsonAsync($"/api/wallet/{userId}/deposit", deposit2Request);

        var withdrawRequest = new WithdrawRequest
        {
            UserId = userId,
            Amount = 300m,
            Currency = "USD",
            TransactionId = Guid.NewGuid().ToString()
        };
        await _client.PostAsJsonAsync($"/api/wallet/{userId}/withdraw", withdrawRequest);

        var balanceResponse = await _client.GetAsync($"/api/wallet/{userId}/balance");
        var balanceResult = await balanceResponse.Content.ReadFromJsonAsync<BalanceResponse>();

        var transactionsResponse = await _client.GetAsync($"/api/wallet/{userId}/transactions");
        var transactionsResult = await transactionsResponse.Content.ReadFromJsonAsync<TransactionsResponse>();

        balanceResult!.Amount.Should().Be(1200m);
        balanceResult.AvailableAmount.Should().Be(1200m);

        transactionsResult!.Transactions.Should().HaveCount(3);
        transactionsResult.Transactions.All(t => t.Status == "Completed").Should().BeTrue();
        
        var deposits = transactionsResult.Transactions.Where(t => t.Type == "Deposit").ToList();
        var withdrawals = transactionsResult.Transactions.Where(t => t.Type == "Withdrawal").ToList();
        
        deposits.Should().HaveCount(2);
        withdrawals.Should().HaveCount(1);
        
        deposits.Sum(d => d.Amount).Should().Be(1500m);
        withdrawals.Sum(w => w.Amount).Should().Be(300m);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DepositEndpoint_WithInvalidUserId_ShouldReturnBadRequest(string invalidUserId)
    {
        var request = new DepositRequest
        {
            UserId = invalidUserId,
            Amount = 100m,
            Currency = "USD",
            TransactionId = Guid.NewGuid().ToString()
        };

        var response = await _client.PostAsJsonAsync($"/api/wallet/{invalidUserId}/deposit", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("DOLLAR")]
    [InlineData("")]
    public async Task DepositEndpoint_WithInvalidCurrency_ShouldReturnBadRequest(string invalidCurrency)
    {
        var userId = "api_user_011";
        var request = new DepositRequest
        {
            UserId = userId,
            Amount = 100m,
            Currency = invalidCurrency,
            TransactionId = Guid.NewGuid().ToString()
        };

        var response = await _client.PostAsJsonAsync($"/api/wallet/{userId}/deposit", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DepositEndpoint_WithExcessiveAmount_ShouldReturnBadRequest()
    {
        var userId = "api_user_012";
        var request = new DepositRequest
        {
            UserId = userId,
            Amount = 10_000_000m,
            Currency = "USD",
            TransactionId = Guid.NewGuid().ToString()
        };

        var response = await _client.PostAsJsonAsync($"/api/wallet/{userId}/deposit", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}