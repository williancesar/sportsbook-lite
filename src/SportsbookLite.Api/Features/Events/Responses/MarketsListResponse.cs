namespace SportsbookLite.Api.Features.Events.Responses;

public sealed record MarketsListResponse
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<MarketDto> Markets { get; init; } = Array.Empty<MarketDto>();
}