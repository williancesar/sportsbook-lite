namespace SportsbookLite.Contracts.Wallet;

[GenerateSerializer]
public readonly record struct Money([property: Id(0)] decimal Amount, [property: Id(1)] string Currency)
{
    public static Money Zero(string currency = "USD") => new(0m, currency);
    
    public static Money Create(decimal amount, string currency = "USD")
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative", nameof(amount));
        
        return new Money(amount, currency);
    }
    
    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot add different currencies: {Currency} and {other.Currency}");
        
        return new Money(Amount + other.Amount, Currency);
    }
    
    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot subtract different currencies: {Currency} and {other.Currency}");
        
        return new Money(Amount - other.Amount, Currency);
    }
    
    public bool IsGreaterThan(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot compare different currencies: {Currency} and {other.Currency}");
        
        return Amount > other.Amount;
    }
    
    public bool IsGreaterThanOrEqualTo(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot compare different currencies: {Currency} and {other.Currency}");
        
        return Amount >= other.Amount;
    }
}