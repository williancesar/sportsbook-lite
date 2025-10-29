namespace SportsbookLite.Api.Features.Odds.Requests;

public sealed class SuspendOddsRequest
{
    public string Reason { get; set; } = string.Empty;
    public string? SuspendedBy { get; set; }
}