namespace SportsbookLite.Contracts.Odds;

[GenerateSerializer]
public readonly record struct OddsUpdate(
    [property: Id(0)] Odds PreviousOdds,
    [property: Id(1)] Odds NewOdds,
    [property: Id(2)] OddsSource UpdateSource,
    [property: Id(3)] DateTimeOffset UpdatedAt,
    [property: Id(4)] string? Reason = null)
{
    public static OddsUpdate Create(Odds previousOdds, Odds newOdds, OddsSource updateSource, string? reason = null)
    {
        return new OddsUpdate(
            PreviousOdds: previousOdds,
            NewOdds: newOdds,
            UpdateSource: updateSource,
            UpdatedAt: DateTimeOffset.UtcNow,
            Reason: reason);
    }

    public decimal PercentageChange => PreviousOdds.Decimal != 0 
        ? Math.Abs((NewOdds.Decimal - PreviousOdds.Decimal) / PreviousOdds.Decimal) * 100
        : 100;

    public bool IsSignificantChange(decimal threshold = 5.0m) => PercentageChange >= threshold;

    public OddsUpdateDirection Direction => NewOdds.Decimal > PreviousOdds.Decimal 
        ? OddsUpdateDirection.Lengthened
        : NewOdds.Decimal < PreviousOdds.Decimal 
            ? OddsUpdateDirection.Shortened 
            : OddsUpdateDirection.NoChange;
}

public enum OddsUpdateDirection
{
    NoChange = 0,
    Shortened = 1,
    Lengthened = 2
}