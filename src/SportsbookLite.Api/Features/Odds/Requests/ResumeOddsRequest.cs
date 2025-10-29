namespace SportsbookLite.Api.Features.Odds.Requests;

public sealed class ResumeOddsRequest
{
    public string Reason { get; set; } = string.Empty;
    public string? ResumedBy { get; set; }
}