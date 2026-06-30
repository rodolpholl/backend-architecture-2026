using Bogus;

namespace FinControl.StressTests.Fakers;

// Amount in cents (long), as per RegisterTransactionCommand.Amount
// Credit categories (amount > 0): Sale=1, CashSupply=3, DebtCollection=6
// Debit categories (amount < 0): Return=2, CashWithdrawal=4, SupplierPayment=5

internal static class TransactionFaker
{
    private static readonly Faker Faker = new("pt_BR");

    private static readonly int[] CreditCategories = [1, 6];
    private static readonly int[] DebitCategories  = [2, 3, 4, 5];

    public static TransactionRequest NextCredit() => new(
        Category:      Faker.PickRandom(CreditCategories),
        Amount:           Faker.Random.Long(100, 1_000_000),   // R$ 1,00 a R$ 10.000,00
        Description:       null,
        TransactionDate:  DateTimeOffset.UtcNow,
        UserId:       Guid.NewGuid().ToString(),
        UserName:     "Stress Test",
        UserEmail:    "stress@fincontrol.test",
        IdempotencyKey:  Guid.NewGuid(),
        CorrelationId:   Guid.NewGuid());

    public static TransactionRequest NextDebit() => new(
        Category:      Faker.PickRandom(DebitCategories),
        Amount:           Faker.Random.Long(-1_000_000, -100),
        Description:       null,
        TransactionDate:  DateTimeOffset.UtcNow,
        UserId:       Guid.NewGuid().ToString(),
        UserName:     "Stress Test",
        UserEmail:    "stress@fincontrol.test",
        IdempotencyKey:  Guid.NewGuid(),
        CorrelationId:   Guid.NewGuid());

    public static TransactionRequest Next() =>
        Faker.Random.Bool() ? NextCredit() : NextDebit();
}
