namespace SportsbookLite.Contracts.Odds;

[GenerateSerializer]
public readonly record struct Odds(
    [property: Id(0)] decimal Decimal,
    [property: Id(1)] string MarketId,
    [property: Id(2)] string Selection,
    [property: Id(3)] OddsSource Source,
    [property: Id(4)] DateTimeOffset Timestamp)
{
    public static Odds Create(decimal decimalOdds, string marketId, string selection, OddsSource source = OddsSource.Manual)
    {
        if (decimalOdds <= 0)
            throw new ArgumentException("Decimal odds must be greater than zero", nameof(decimalOdds));
        
        return new Odds(
            Decimal: Math.Round(decimalOdds, 2),
            MarketId: marketId,
            Selection: selection,
            Source: source,
            Timestamp: DateTimeOffset.UtcNow);
    }

    public decimal ToFractional()
    {
        return Decimal - 1;
    }

    public int ToAmerican()
    {
        if (Decimal >= 2.0m)
            return (int)Math.Round((Decimal - 1) * 100);
        else
            return (int)Math.Round(-100 / (Decimal - 1));
    }

    public static Odds FromFractional(decimal fractional, string marketId, string selection, OddsSource source = OddsSource.Manual)
    {
        if (fractional < 0)
            throw new ArgumentException("Fractional odds cannot be negative", nameof(fractional));
        
        return Create(fractional + 1, marketId, selection, source);
    }

    public static Odds FromAmerican(int american, string marketId, string selection, OddsSource source = OddsSource.Manual)
    {
        if (american == 0)
            throw new ArgumentException("American odds cannot be zero", nameof(american));

        decimal decimalOdds = american > 0 
            ? (american / 100m) + 1
            : (100m / Math.Abs(american)) + 1;

        return Create(decimalOdds, marketId, selection, source);
    }

    public decimal CalculateProfit(decimal stake)
    {
        if (stake < 0)
            throw new ArgumentException("Stake cannot be negative", nameof(stake));
        
        return stake * (Decimal - 1);
    }

    public decimal CalculatePayout(decimal stake)
    {
        if (stake < 0)
            throw new ArgumentException("Stake cannot be negative", nameof(stake));
        
        return stake * Decimal;
    }

    public decimal ImpliedProbability => 1 / Decimal;
}