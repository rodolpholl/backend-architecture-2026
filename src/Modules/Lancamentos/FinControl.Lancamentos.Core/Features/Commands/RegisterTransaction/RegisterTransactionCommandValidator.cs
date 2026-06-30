using FinControl.Transactions.Core.Domain.Enums;
using FluentValidation;

namespace FinControl.Transactions.Core.Features.Commands.RegisterTransaction;

/// <summary>
/// Validator para o comando RegisterTransaction usando FluentValidation.
/// Aplica validações de negócio e format de dados.
/// </summary>
public class RegisterTransactionCommandValidator : AbstractValidator<RegisterTransactionCommand>
{
    public RegisterTransactionCommandValidator()
    {
        // Category
        RuleFor(x => x.Category)
            .IsInEnum()
            .WithMessage("Category inválida. Deve ser uma das modalidades disponíveis.");

        // Amount
        RuleFor(x => x.Amount)
            .LessThanOrEqualTo(99999999999999) // ~999.999.999.999,99
            .WithMessage("Amount máximo permitido é R$ 999.999.999.999,99.")
            
            .Must((model, v) => model.Category == TransactionCategory.Sale ||
                                model.Category == TransactionCategory.DebtCollection
                                 ? v >= 0 : true)
            .WithMessage("Amount deve ser maior que zero para modalidades informada.")

            .Must((model, v) => model.Category == TransactionCategory.Return ||
                                model.Category == TransactionCategory.CashSupply ||
                                model.Category == TransactionCategory.SupplierPayment ||
                                model.Category == TransactionCategory.CashWithdrawal ? v < 0 : true)
            .WithMessage("Amount deve ser menor que zero para modalidades informada.");

        

        // Descrição - obrigatória se Category = Outros
        RuleFor(x => x.Description)
            .NotEmpty()
            .When(x => x.Category == TransactionCategory.Others)
            .WithMessage("Descrição é obrigatória para lançamentos do tipo 'Outros'.");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Description))
            .WithMessage("Descrição não pode exceder 500 caracteres.");

        // Data do Lançamento — avaliadas em runtime (não capturadas no construtor)
        RuleFor(x => x.TransactionDate)
            .Must(d => d <= DateTimeOffset.UtcNow.AddDays(1))
            .WithMessage("Data do lançamento não pode ser no futuro (máximo 1 dia a frente).");

        RuleFor(x => x.TransactionDate)
            .Must(d => d >= DateTimeOffset.UtcNow.AddYears(-1))
            .When(x => x.TransactionDate != default)
            .WithMessage("Data do lançamento não pode ser anterior a 1 ano.");

        // UserId
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("ID do usuário é obrigatório.");

        RuleFor(x => x.UserId)
            .Length(36) // UUID format
            .When(x => !string.IsNullOrWhiteSpace(x.UserId))
            .WithMessage("ID do usuário deve ser um UUID válido.");

        // UserName
        RuleFor(x => x.UserName)
            .NotEmpty()
            .WithMessage("Nome do usuário é obrigatório.")
            .MaximumLength(200)
            .WithMessage("Nome do usuário não pode exceder 200 caracteres.");

        // UserEmail
        RuleFor(x => x.UserEmail)
            .NotEmpty()
            .WithMessage("Email do usuário é obrigatório.")
            .EmailAddress()
            .WithMessage("Email do usuário deve ser um endereço válido.")
            .MaximumLength(200)
            .WithMessage("Email não pode exceder 200 caracteres.");

        // IdempotencyKey
        RuleFor(x => x.IdempotencyKey)
            .NotEqual(Guid.Empty)
            .WithMessage("Chave de idempotência inválida.");

        // CorrelationId
        RuleFor(x => x.CorrelationId)
            .NotEqual(Guid.Empty)
            .WithMessage("ID de correlação inválido.");
    }
}
