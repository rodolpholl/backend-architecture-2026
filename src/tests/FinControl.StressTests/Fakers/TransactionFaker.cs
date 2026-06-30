using Bogus;

namespace FinControl.StressTests.Fakers;

// Amount em centavos (long), conforme RegisterTransactionCommand.Amount
// Categorys crédito (valor > 0): Venda=1, Suprimento=3, RecebimentoDivida=6
// Categorys débito  (valor < 0): Devolucao=2, Sangria=4, PagamentoFornecedor=5

internal static class TransactionFaker
{
    private static readonly Faker Faker = new("pt_BR");

    private static readonly int[] CategorysCredito = [1, 6];
    private static readonly int[] CategorysDebito  = [2, 3, 4, 5];

    public static LancamentoRequest NextCredito() => new(
        Category:      Faker.PickRandom(CategorysCredito),
        Amount:           Faker.Random.Long(100, 1_000_000),   // R$ 1,00 a R$ 10.000,00
        Description:       null,
        TransactionDate:  DateTimeOffset.UtcNow,
        UserId:       Guid.NewGuid().ToString(),
        UserName:     "Stress Test",
        UserEmail:    "stress@fincontrol.test",
        IdempotencyKey:  Guid.NewGuid(),
        CorrelationId:   Guid.NewGuid());

    public static LancamentoRequest NextDebito() => new(
        Category:      Faker.PickRandom(CategorysDebito),
        Amount:           Faker.Random.Long(-1_000_000, -100),
        Description:       null,
        TransactionDate:  DateTimeOffset.UtcNow,
        UserId:       Guid.NewGuid().ToString(),
        UserName:     "Stress Test",
        UserEmail:    "stress@fincontrol.test",
        IdempotencyKey:  Guid.NewGuid(),
        CorrelationId:   Guid.NewGuid());

    public static LancamentoRequest Next() =>
        Faker.Random.Bool() ? NextCredito() : NextDebito();
}
