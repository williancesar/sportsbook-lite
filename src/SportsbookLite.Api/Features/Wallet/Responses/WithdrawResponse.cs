namespace SportsbookLite.Api.Features.Wallet.Responses;

public sealed record WithdrawResponse
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public TransactionResponseDto? Transaction { get; init; }
    public MoneyResponseDto? NewBalance { get; init; }
}