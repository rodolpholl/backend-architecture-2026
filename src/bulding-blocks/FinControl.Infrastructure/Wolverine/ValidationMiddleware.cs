using FluentValidation;
using FluentValidation.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace FinControl.Infrastructure.Wolverine;

/// <summary>
/// Middleware Wolverine de validação automática via FluentValidation.
///
/// Usa Envelope (tipo nativo Wolverine) e reflexão para resolver IValidator&lt;T&gt;
/// sem precisar de parâmetro genérico T no método Before.
/// Parâmetros genéricos T em middleware causam UnResolvableVariableException no JasperFx code gen.
/// </summary>
public sealed class FluentValidationMiddleware(IServiceProvider services)
{
    public async Task Before(Envelope envelope, ILogger<FluentValidationMiddleware> logger, CancellationToken ct)
    {
        var message = envelope.Message;
        if (message is null)
            return;

        var messageType = message.GetType();
        var validatorType = typeof(IValidator<>).MakeGenericType(messageType);

        if (services.GetService(validatorType) is not IValidator validator)
            return;

        var contextType = typeof(ValidationContext<>).MakeGenericType(messageType);
        var validationContext = (IValidationContext)Activator.CreateInstance(contextType, message)!;

        var result = await validator.ValidateAsync(validationContext, ct);
        if (result.IsValid)
            return;

        var erros = result.Errors
            .Select(f => $"{f.PropertyName}: {f.ErrorMessage}")
            .ToArray();

        logger.LogWarning(
            "Validação falhou para {MessageType}. Erros: [{Errors}]",
            messageType.Name,
            string.Join(" | ", erros));

        throw new ValidationException(result.Errors);
    }
}
