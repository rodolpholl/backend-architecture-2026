using FluentAssertions;
using FinControl.Entries.Core.Domain;
using FinControl.Entries.Core.Domain.Enums;

namespace FinControl.Entries.Tests.Domain;

public class TransactionTests
{
    [Fact]
    public void Type_Should_Be_Credit_For_Positive_Amount()
    {
        var transaction = new Transaction { Amount = 1000, Category = TransactionCategory.Sale };

        transaction.Type.Should().Be(TransactionType.Credit);
    }

    [Fact]
    public void Type_Should_Be_Debit_For_Negative_Amount()
    {
        var transaction = new Transaction { Amount = -1000, Category = TransactionCategory.Return };

        transaction.Type.Should().Be(TransactionType.Debit);
    }

    [Theory]
    [InlineData(15550, 155.50)]
    [InlineData(100, 1.00)]
    [InlineData(1, 0.01)]
    [InlineData(100000, 1000.00)]
    public void FormattedAmount_Should_Convert_Cents_To_Reals(long cents, double expected)
    {
        var transaction = new Transaction { Amount = cents };

        transaction.FormattedAmount.Should().Be((decimal)expected);
    }

    [Fact]
    public void FormattedAmount_Should_Maintain_Negative_Sign()
    {
        var transaction = new Transaction { Amount = -5000 };

        transaction.FormattedAmount.Should().Be(-50.00m);
    }

    [Fact]
    public void Two_Transactions_With_Same_Id_Should_Be_Equal()
    {
        var a = new Transaction { Category = TransactionCategory.Sale, Amount = 1000 };
        var b = new Transaction { Category = TransactionCategory.Return, Amount = -500 };

        // Default Id is 0 (default long) in both — equality by Id
        a.Should().Be(b);
    }

    [Fact]
    public void Transaction_Should_Have_CreatedAt_Populated_By_Default()
    {
        var before = DateTimeOffset.UtcNow;
        var transaction = new Transaction { Amount = 500, Category = TransactionCategory.Sale };
        var after = DateTimeOffset.UtcNow;

        transaction.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}

