using FinControl.Entries.Core.Domain.Enums;
using FluentValidation;

namespace FinControl.Entries.Core.Features.Commands.RegisterTransaction;

/// <summary>
/// Validator for the RegisterTransaction command using FluentValidation.
/// Applies business rules and data format validations.
/// </summary>
public class RegisterTransactionCommandValidator : AbstractValidator<RegisterTransactionCommand>
{
    public RegisterTransactionCommandValidator()
    {
        // Category
        RuleFor(x => x.Category)
            .IsInEnum()
            .WithMessage("Invalid category. Must be one of the available transaction types.");

        // Amount
        RuleFor(x => x.Amount)
            .LessThanOrEqualTo(99999999999999) // ~999.999.999.999,99
            .WithMessage("Maximum amount allowed is R$ 999.999.999.999,99.")
            
            .Must((model, v) => model.Category == TransactionCategory.Sale ||
                                model.Category == TransactionCategory.DebtCollection
                                 ? v >= 0 : true)
            .WithMessage("Amount must be greater than zero for the specified transaction type.")

            .Must((model, v) => model.Category == TransactionCategory.Return ||
                                model.Category == TransactionCategory.CashSupply ||
                                model.Category == TransactionCategory.SupplierPayment ||
                                model.Category == TransactionCategory.CashWithdrawal ? v < 0 : true)
            .WithMessage("Amount must be less than zero for the specified transaction type.");

        

        // Description - required if Category = Others
        RuleFor(x => x.Description)
            .NotEmpty()
            .When(x => x.Category == TransactionCategory.Others)
            .WithMessage("Description is required for transactions of type 'Others'.");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Description))
            .WithMessage("Description cannot exceed 500 characters.");

        // Transaction Date — evaluated at runtime (not captured in constructor)
        RuleFor(x => x.TransactionDate)
            .Must(d => d <= DateTimeOffset.UtcNow.AddDays(1))
            .WithMessage("Transaction date cannot be in the future (maximum 1 day ahead).");

        RuleFor(x => x.TransactionDate)
            .Must(d => d >= DateTimeOffset.UtcNow.AddYears(-1))
            .When(x => x.TransactionDate != default)
            .WithMessage("Transaction date cannot be more than 1 year in the past.");

        // UserId
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");

        RuleFor(x => x.UserId)
            .Length(36) // UUID format
            .When(x => !string.IsNullOrWhiteSpace(x.UserId))
            .WithMessage("User ID must be a valid UUID.");

        // UserName
        RuleFor(x => x.UserName)
            .NotEmpty()
            .WithMessage("User name is required.")
            .MaximumLength(200)
            .WithMessage("User name cannot exceed 200 characters.");

        // UserEmail
        RuleFor(x => x.UserEmail)
            .NotEmpty()
            .WithMessage("User email is required.")
            .EmailAddress()
            .WithMessage("User email must be a valid address.")
            .MaximumLength(200)
            .WithMessage("Email cannot exceed 200 characters.");

        // IdempotencyKey
        RuleFor(x => x.IdempotencyKey)
            .NotEqual(Guid.Empty)
            .WithMessage("Invalid idempotency key.");

        // CorrelationId
        RuleFor(x => x.CorrelationId)
            .NotEqual(Guid.Empty)
            .WithMessage("Invalid correlation ID.");
    }
}

