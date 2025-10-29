namespace SportsbookLite.Api.Features.Odds.Requests;

public sealed class UpdateOddsRequest
{
    public Dictionary<string, decimal> Selections { get; set; } = new();
    public string? Reason { get; set; }
    public string? UpdatedBy { get; set; }
}