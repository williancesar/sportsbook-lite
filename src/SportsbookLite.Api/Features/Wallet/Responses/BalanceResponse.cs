namespace SportsbookLite.Api.Features.Wallet.Responses;

public sealed record BalanceResponse
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
    public decimal AvailableAmount { get; init; }
    public string UserId { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}