using Bogus;

namespace SportsbookLite.TestUtilities.TestDataBuilders;

public abstract class TestDataBuilder<T> where T : class
{
    protected readonly Faker Faker = new();
    
    public abstract T Build();
    
    public virtual IEnumerable<T> Build(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return Build();
        }
    }
    
    public virtual List<T> BuildList(int count) => Build(count).ToList();
}