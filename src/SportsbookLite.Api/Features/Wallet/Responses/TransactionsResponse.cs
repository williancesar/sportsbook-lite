namespace SportsbookLite.Api.Features.Wallet.Responses;

public sealed record TransactionsResponse
{
    public IReadOnlyList<TransactionResponseDto> Transactions { get; init; } = Array.Empty<TransactionResponseDto>();
    public int Count { get; init; }
    public int Offset { get; init; }
    public int Limit { get; init; }
    public bool HasMore { get; init; }
}