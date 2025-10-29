namespace SportsbookLite.Api.Features.Wallet.Responses;

public sealed record DepositResponse
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public TransactionResponseDto? Transaction { get; init; }
    public MoneyResponseDto? NewBalance { get; init; }
}

public sealed record MoneyResponseDto
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
}

public sealed record TransactionResponseDto
{
    public string Id { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
    public string Status { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public string? ReferenceId { get; init; }
    public string? ErrorMessage { get; init; }
}