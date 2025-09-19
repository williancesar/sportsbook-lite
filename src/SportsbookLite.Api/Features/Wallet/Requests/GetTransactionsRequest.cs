using System.ComponentModel.DataAnnotations;

namespace SportsbookLite.Api.Features.Wallet.Requests;

public sealed record GetTransactionsRequest
{
    [Required]
    public string UserId { get; init; } = string.Empty;

    [Range(1, 1000, ErrorMessage = "Limit must be between 1 and 1000")]
    public int Limit { get; init; } = 50;

    public int Offset { get; init; } = 0;
}