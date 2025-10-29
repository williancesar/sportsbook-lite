using Bogus;

namespace SportsbookLite.TestUtilities.TestDataBuilders;

public static class CommonTestData
{
    private static readonly Faker Faker = new();

    public static class Identifiers
    {
        public static Guid UserId => Faker.Random.Guid();
        public static Guid BetId => Faker.Random.Guid();
        public static Guid EventId => Faker.Random.Guid();
        public static Guid MarketId => Faker.Random.Guid();
        public static string TransactionId => Faker.Random.AlphaNumeric(10);
    }

    public static class Financial
    {
        public static decimal Amount => Faker.Random.Decimal(1m, 10000m);
        public static decimal Balance => Faker.Random.Decimal(0m, 100000m);
        public static decimal Odds => Math.Round(Faker.Random.Decimal(1.1m, 50.0m), 2);
        public static decimal Stake => Faker.Random.Decimal(5m, 1000m);
    }

    public static class Sports
    {
        private static readonly string[] TeamNames = 
        {
            "Manchester United", "Liverpool", "Arsenal", "Chelsea", "Real Madrid",
            "Barcelona", "Bayern Munich", "PSG", "Juventus", "AC Milan"
        };

        private static readonly string[] SportTypes = 
        {
            "Football", "Basketball", "Tennis", "Baseball", "Hockey"
        };

        public static string TeamName => Faker.Random.ArrayElement(TeamNames);
        public static string SportType => Faker.Random.ArrayElement(SportTypes);
        public static string EventName => $"{TeamName} vs {TeamName}";
        public static DateTime EventDate => Faker.Date.Future();
    }

    public static class Users
    {
        public static string Username => Faker.Internet.UserName();
        public static string Email => Faker.Internet.Email();
        public static string FirstName => Faker.Name.FirstName();
        public static string LastName => Faker.Name.LastName();
    }

    public static class Addresses
    {
        public static string Country => Faker.Address.Country();
        public static string City => Faker.Address.City();
        public static string State => Faker.Address.State();
        public static string PostalCode => Faker.Address.ZipCode();
    }
}