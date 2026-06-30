using Bogus;
using FinControl.Entries.Core.Domain.Enums;
using FinControl.Entries.Core.Features.Commands.RegisterTransaction;

namespace FinControl.Entries.Tests.Fakers;

public static class TransactionCommandFaker
{
    private static readonly Faker Faker = new("pt_BR");

    public static RegisterTransactionCommand ValidVenda(long? valor = null) =>
        Build(TransactionCategory.Sale, valor ?? Faker.Random.Long(100, 999_999));

    public static RegisterTransactionCommand ValidDebito(
        TransactionCategory modalidade = TransactionCategory.Return,
        long? valor = null) =>
        Build(modalidade, valor ?? -Faker.Random.Long(100, 999_999));

    public static RegisterTransactionCommand ValidOutros(string? descricao = null) =>
        Build(TransactionCategory.Others, Faker.Random.Long(100, 999_999),
              descricao ?? Faker.Commerce.ProductDescription()[..Math.Min(100, Faker.Commerce.ProductDescription().Length)]);

    public static RegisterTransactionCommand Build(
        TransactionCategory modalidade,
        long valor,
        string? descricao = null) =>
        new()
        {
            Category = modalidade,
            Amount = valor,
            Description = descricao ?? Faker.Commerce.ProductDescription()[..50],
            TransactionDate = DateTimeOffset.UtcNow.AddDays(-Faker.Random.Int(0, 30)),
            UserId = Guid.NewGuid().ToString(),
            UserName = Faker.Name.FullName(),
            UserEmail = Faker.Internet.Email(),
            IdempotencyKey = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid()
        };
}

