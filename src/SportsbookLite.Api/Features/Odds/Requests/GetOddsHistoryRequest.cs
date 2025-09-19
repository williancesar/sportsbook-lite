namespace SportsbookLite.Api.Features.Odds.Requests;

public sealed class GetOddsHistoryRequest
{
    public string? Selection { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public int Limit { get; set; } = 100;
}