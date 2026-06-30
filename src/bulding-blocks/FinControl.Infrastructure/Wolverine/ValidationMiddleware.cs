using FluentValidation;
using FluentValidation.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace FinControl.Infrastructure.Wolverine;

/// <summary>
/// Wolverine middleware for automatic validation via FluentValidation.
///
/// Uses Envelope (Wolverine native type) and reflection to resolve IValidator&lt;T&gt;
/// without needing generic parameter T in the Before method.
/// Generic parameters T in middleware cause UnResolvableVariableException in JasperFx code gen.
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

        var errors = result.Errors
            .Select(f => $"{f.PropertyName}: {f.ErrorMessage}")
            .ToArray();

        logger.LogWarning(
            "Validation failed for {MessageType}. Errors: [{Errors}]",
            messageType.Name,
            string.Join(" | ", errors));

        throw new ValidationException(result.Errors);
    }
}
