using System.ComponentModel.DataAnnotations;

namespace SportsbookLite.Api.Features.Betting.Requests;

public sealed record GetUserBetsRequest
{
    [Required]
    public string UserId { get; init; } = string.Empty;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100")]
    public int PageSize { get; init; } = 20;

    [Range(1, int.MaxValue, ErrorMessage = "Page number must be greater than 0")]
    public int Page { get; init; } = 1;

    public string? Status { get; init; }

    public DateTimeOffset? FromDate { get; init; }

    public DateTimeOffset? ToDate { get; init; }
}