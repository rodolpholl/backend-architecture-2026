using FluentAssertions;
using FinControl.Entries.Core.Domain.Enums;
using FinControl.Entries.Core.Features.Commands.RegisterTransaction;
using FinControl.Entries.Tests.Fakers;

namespace FinControl.Entries.Tests.Features.Commands;

public class RegisterTransactionCommandValidatorTests
{
    private readonly RegisterTransactionCommandValidator _validator = new();

    // ── Happy path ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TransactionCategory.Sale, 1000)]
    [InlineData(TransactionCategory.DebtCollection, 500)]
    public void Should_Pass_For_Credits_With_Positive_Amount(TransactionCategory category, long amount)
    {
        var result = _validator.Validate(TransactionCommandFaker.Build(category, amount));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(TransactionCategory.Return, -500)]
    [InlineData(TransactionCategory.CashSupply, -200)]
    [InlineData(TransactionCategory.SupplierPayment, -1500)]
    [InlineData(TransactionCategory.CashWithdrawal, -300)]
    public void Should_Pass_For_Debits_With_Negative_Amount(TransactionCategory category, long amount)
    {
        var result = _validator.Validate(TransactionCommandFaker.Build(category, amount));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_Pass_For_Others_With_Description()
    {
        var result = _validator.Validate(TransactionCommandFaker.ValidOutros("Ad hoc service payment"));

        result.IsValid.Should().BeTrue();
    }

    // ── Category ──────────────────────────────────────────────────────────

    [Fact]
    public void Should_Fail_When_Category_Invalid()
    {
        var command = TransactionCommandFaker.ValidVenda() with { Category = (TransactionCategory)99 };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Category));
    }

    // ── Amount ───────────────────────────────────────────────────────────────

    [Fact]
    public void Should_Fail_When_Amount_Exceeds_Maximum()
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
    public void Should_Fail_When_Positive_Amount_For_Debit_Category(TransactionCategory category, long amount)
    {
        var command = TransactionCommandFaker.Build(category, amount);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Amount));
    }

    [Theory]
    [InlineData(TransactionCategory.Sale, -500)]
    [InlineData(TransactionCategory.DebtCollection, -100)]
    public void Should_Fail_When_Negative_Amount_For_Credit_Category(TransactionCategory category, long amount)
    {
        var command = TransactionCommandFaker.Build(category, amount);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Amount));
    }

    // ── Description ───────────────────────────────────────────────────────────

    [Fact]
    public void Should_Fail_When_Others_Category_Without_Description()
    {
        var command = TransactionCommandFaker.Build(TransactionCategory.Others, 500, descricao: null)
            with { Description = null };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Description));
    }

    [Fact]
    public void Should_Fail_When_Description_Exceeds_500_Characters()
    {
        var command = TransactionCommandFaker.ValidVenda() with { Description = new string('x', 501) };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Description));
    }

    // ── Date ────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_Fail_When_TransactionDate_More_Than_1_Day_In_Future()
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
    public void Should_Fail_When_TransactionDate_More_Than_One_Year_Ago()
    {
        var command = TransactionCommandFaker.ValidVenda() with
        {
            TransactionDate = DateTimeOffset.UtcNow.AddYears(-2)
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.TransactionDate));
    }

    // ── User ─────────────────────────────────────────────────────────────

    [Fact]
    public void Should_Fail_When_UserId_Empty()
    {
        var command = TransactionCommandFaker.ValidVenda() with { UserId = string.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.UserId));
    }

    [Fact]
    public void Should_Fail_When_UserId_Not_36_Characters()
    {
        var command = TransactionCommandFaker.ValidVenda() with { UserId = "short-user" };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.UserId));
    }

    [Fact]
    public void Should_Fail_When_UserName_Empty()
    {
        var command = TransactionCommandFaker.ValidVenda() with { UserName = string.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.UserName));
    }

    [Fact]
    public void Should_Fail_When_UserName_Exceeds_200_Characters()
    {
        var command = TransactionCommandFaker.ValidVenda() with { UserName = new string('a', 201) };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.UserName));
    }

    // ── Email ───────────────────────────────────────────────────────────────

    [Fact]
    public void Should_Fail_When_Email_Invalid()
    {
        var command = TransactionCommandFaker.ValidVenda() with { UserEmail = "not-an-email" };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.UserEmail));
    }

    [Fact]
    public void Should_Fail_When_Email_Empty()
    {
        var command = TransactionCommandFaker.ValidVenda() with { UserEmail = string.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.UserEmail));
    }

    // ── Idempotency / Correlation ────────────────────────────────────────────

    [Fact]
    public void Should_Fail_When_IdempotencyKey_Is_Empty_Guid()
    {
        var command = TransactionCommandFaker.ValidVenda() with { IdempotencyKey = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.IdempotencyKey));
    }

    [Fact]
    public void Should_Fail_When_CorrelationId_Is_Empty_Guid()
    {
        var command = TransactionCommandFaker.ValidVenda() with { CorrelationId = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.CorrelationId));
    }
}

