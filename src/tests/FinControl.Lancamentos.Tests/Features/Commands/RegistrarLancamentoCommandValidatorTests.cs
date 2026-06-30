using FluentAssertions;
using FinControl.Transactions.Core.Domain.Enums;
using FinControl.Transactions.Core.Features.Commands.RegisterTransaction;
using FinControl.Transactions.Tests.Fakers;

namespace FinControl.Transactions.Tests.Features.Commands;

public class RegisterTransactionCommandValidatorTests
{
    private readonly RegisterTransactionCommandValidator _validator = new();

    // ── Happy path ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TransactionCategory.Sale, 1000)]
    [InlineData(TransactionCategory.DebtCollection, 500)]
    public void Deve_Passar_Para_Creditos_Com_Valor_Positivo(TransactionCategory modalidade, long valor)
    {
        var result = _validator.Validate(TransactionCommandFaker.Build(modalidade, valor));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(TransactionCategory.Return, -500)]
    [InlineData(TransactionCategory.CashSupply, -200)]
    [InlineData(TransactionCategory.SupplierPayment, -1500)]
    [InlineData(TransactionCategory.CashWithdrawal, -300)]
    public void Deve_Passar_Para_Debitos_Com_Valor_Negativo(TransactionCategory modalidade, long valor)
    {
        var result = _validator.Validate(TransactionCommandFaker.Build(modalidade, valor));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Deve_Passar_Para_Outros_Com_Description()
    {
        var result = _validator.Validate(TransactionCommandFaker.ValidOutros("Pagamento avulso de serviço"));

        result.IsValid.Should().BeTrue();
    }

    // ── Category ──────────────────────────────────────────────────────────

    [Fact]
    public void Deve_Falhar_Quando_Category_Invalida()
    {
        var command = TransactionCommandFaker.ValidVenda() with { Category = (TransactionCategory)99 };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Category));
    }

    // ── Amount ───────────────────────────────────────────────────────────────

    [Fact]
    public void Deve_Falhar_Quando_Valor_Excede_Maximo()
    {
        var command = TransactionCommandFaker.ValidVenda() with { Amount = 100_000_000_000_000L };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Amount));
    }

    [Theory]
    [InlineData(TransactionCategory.Return, 500)]
    [InlineData(TransactionCategory.CashSupply, 100)]
    [InlineData(TransactionCategory.SupplierPayment, 1000)]
    [InlineData(TransactionCategory.CashWithdrawal, 750)]
    public void Deve_Falhar_Quando_Valor_Positivo_Para_Category_Debito(TransactionCategory modalidade, long valor)
    {
        var command = TransactionCommandFaker.Build(modalidade, valor);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Amount));
    }

    [Theory]
    [InlineData(TransactionCategory.Sale, -500)]
    [InlineData(TransactionCategory.DebtCollection, -100)]
    public void Deve_Falhar_Quando_Valor_Negativo_Para_Category_Credito(TransactionCategory modalidade, long valor)
    {
        var command = TransactionCommandFaker.Build(modalidade, valor);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Amount));
    }

    // ── Descrição ───────────────────────────────────────────────────────────

    [Fact]
    public void Deve_Falhar_Quando_Category_Outros_Sem_Description()
    {
        var command = TransactionCommandFaker.Build(TransactionCategory.Others, 500, descricao: null)
            with { Description = null };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Description));
    }

    [Fact]
    public void Deve_Falhar_Quando_Description_Excede_500_Caracteres()
    {
        var command = TransactionCommandFaker.ValidVenda() with { Description = new string('x', 501) };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Description));
    }

    // ── Data ────────────────────────────────────────────────────────────────

    [Fact]
    public void Deve_Falhar_Quando_TransactionDate_Mais_De_1_Dia_No_Futuro()
    {
        var command = TransactionCommandFaker.ValidVenda() with
        {
            TransactionDate = DateTimeOffset.UtcNow.AddDays(2)
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.TransactionDate));
    }

    [Fact]
    public void Deve_Falhar_Quando_TransactionDate_Ha_Mais_De_Um_Ano()
    {
        var command = TransactionCommandFaker.ValidVenda() with
        {
            TransactionDate = DateTimeOffset.UtcNow.AddYears(-2)
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.TransactionDate));
    }

    // ── Usuário ─────────────────────────────────────────────────────────────

    [Fact]
    public void Deve_Falhar_Quando_UserId_Vazio()
    {
        var command = TransactionCommandFaker.ValidVenda() with { UserId = string.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.UserId));
    }

    [Fact]
    public void Deve_Falhar_Quando_UserId_Nao_Tem_36_Caracteres()
    {
        var command = TransactionCommandFaker.ValidVenda() with { UserId = "usuario-curto" };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.UserId));
    }

    [Fact]
    public void Deve_Falhar_Quando_UserName_Vazio()
    {
        var command = TransactionCommandFaker.ValidVenda() with { UserName = string.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.UserName));
    }

    [Fact]
    public void Deve_Falhar_Quando_UserName_Excede_200_Caracteres()
    {
        var command = TransactionCommandFaker.ValidVenda() with { UserName = new string('a', 201) };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.UserName));
    }

    // ── Email ───────────────────────────────────────────────────────────────

    [Fact]
    public void Deve_Falhar_Quando_Email_Invalido()
    {
        var command = TransactionCommandFaker.ValidVenda() with { UserEmail = "nao-e-email" };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.UserEmail));
    }

    [Fact]
    public void Deve_Falhar_Quando_Email_Vazio()
    {
        var command = TransactionCommandFaker.ValidVenda() with { UserEmail = string.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.UserEmail));
    }

    // ── Idempotência / Correlação ────────────────────────────────────────────

    [Fact]
    public void Deve_Falhar_Quando_IdempotencyKey_For_Empty_Guid()
    {
        var command = TransactionCommandFaker.ValidVenda() with { IdempotencyKey = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.IdempotencyKey));
    }

    [Fact]
    public void Deve_Falhar_Quando_CorrelationId_For_Empty_Guid()
    {
        var command = TransactionCommandFaker.ValidVenda() with { CorrelationId = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.CorrelationId));
    }
}
