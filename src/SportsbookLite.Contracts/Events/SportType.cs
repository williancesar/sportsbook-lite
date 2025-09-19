namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public enum SportType
{
    [Id(0)]
    Football = 0,
    
    [Id(1)]
    Basketball = 1,
    
    [Id(2)]
    Tennis = 2,
    
    [Id(3)]
    Baseball = 4,
    
    [Id(4)]
    Hockey = 5,
    
    [Id(5)]
    Soccer = 6,
    
    [Id(6)]
    Boxing = 7,
    
    [Id(7)]
    MMA = 8
}