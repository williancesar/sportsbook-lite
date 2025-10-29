namespace SportsbookLite.Api.Features.Odds.Requests;

public sealed class GetVolatilityRequest
{
    public int WindowHours { get; set; } = 1;
}