using FluentAssertions;
using FinControl.Transactions.Core.Domain;
using FinControl.Transactions.Core.Domain.Enums;

namespace FinControl.Transactions.Tests.Domain;

public class TransactionTests
{
    [Fact]
    public void Tipo_Deve_Ser_Credito_Para_Valor_Positivo()
    {
        var lancamento = new Transaction { Amount = 1000, Category = TransactionCategory.Sale };

        lancamento.Type.Should().Be(TransactionType.Credit);
    }

    [Fact]
    public void Tipo_Deve_Ser_Debito_Para_Valor_Negativo()
    {
        var lancamento = new Transaction { Amount = -1000, Category = TransactionCategory.Return };

        lancamento.Type.Should().Be(TransactionType.Debit);
    }

    [Theory]
    [InlineData(15550, 155.50)]
    [InlineData(100, 1.00)]
    [InlineData(1, 0.01)]
    [InlineData(100000, 1000.00)]
    public void FormattedAmount_Deve_Converter_Centavos_Para_Reais(long centavos, double esperado)
    {
        var lancamento = new Transaction { Amount = centavos };

        lancamento.FormattedAmount.Should().Be((decimal)esperado);
    }

    [Fact]
    public void FormattedAmount_Deve_Manter_Sinal_Negativo()
    {
        var lancamento = new Transaction { Amount = -5000 };

        lancamento.FormattedAmount.Should().Be(-50.00m);
    }

    [Fact]
    public void Dois_Transactions_Com_Mesmo_Id_Devem_Ser_Iguais()
    {
        var a = new Transaction { Category = TransactionCategory.Sale, Amount = 1000 };
        var b = new Transaction { Category = TransactionCategory.Return, Amount = -500 };

        // Id padrão é 0 (default long) em ambos — igualdade por Id
        a.Should().Be(b);
    }

    [Fact]
    public void Transaction_Deve_Ter_CreatedAt_Preenchido_Por_Padrao()
    {
        var antes = DateTimeOffset.UtcNow;
        var lancamento = new Transaction { Amount = 500, Category = TransactionCategory.Sale };
        var depois = DateTimeOffset.UtcNow;

        lancamento.CreatedAt.Should().BeOnOrAfter(antes).And.BeOnOrBefore(depois);
    }
}
