using SportsbookLite.Contracts.Odds;

namespace SportsbookLite.Api.Features.Odds.Responses;

public sealed class OddsHistoryResponse
{
    public string MarketId { get; set; } = string.Empty;
    public string Selection { get; set; } = string.Empty;
    public List<OddsUpdateDto> Updates { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastModified { get; set; }
    public int TotalUpdates { get; set; }
    public OddsDto? CurrentOdds { get; set; }
}

public sealed class OddsUpdateDto
{
    public OddsDto? PreviousOdds { get; set; }
    public OddsDto NewOdds { get; set; } = new();
    public decimal PercentageChange { get; set; }
    public OddsSource Source { get; set; }
    public string? UpdateReason { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}