using System.ComponentModel.DataAnnotations;

namespace SportsbookLite.Api.Features.Wallet.Requests;

public sealed record WithdrawRequest
{
    [Required]
    public string UserId { get; init; } = string.Empty;

    [Required]
    [Range(0.01, 1000000.00, ErrorMessage = "Amount must be between 0.01 and 1,000,000")]
    public decimal Amount { get; init; }

    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be exactly 3 characters")]
    public string Currency { get; init; } = "USD";

    [Required]
    public string TransactionId { get; init; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; init; }
}