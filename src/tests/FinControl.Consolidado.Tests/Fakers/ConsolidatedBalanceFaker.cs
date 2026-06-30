using Bogus;
using FinControl.Consolidated.Core.Domain;

namespace FinControl.Consolidated.Tests.Fakers;

public static class ConsolidatedBalanceFaker
{
    private static readonly Faker Faker = new("pt_BR");

    public static ConsolidatedBalance Positivo(long? saldo = null) =>
        new(saldo ?? Faker.Random.Long(100, 999_999),
            DateTimeOffset.UtcNow.AddMinutes(-Faker.Random.Int(1, 60)));

    public static ConsolidatedBalance Negativo(long? saldo = null) =>
        new(saldo ?? -Faker.Random.Long(100, 999_999),
            DateTimeOffset.UtcNow.AddMinutes(-Faker.Random.Int(1, 60)));

    public static ConsolidatedBalance Zero() =>
        new(0, DateTimeOffset.UtcNow);
}
