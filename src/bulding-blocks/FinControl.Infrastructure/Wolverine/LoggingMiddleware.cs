using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Wolverine;

namespace FinControl.Infrastructure.Wolverine;

/// <summary>
/// Cascade Between Before → After para latência e nome do tipo.
/// </summary>
public sealed record LogEntry(string MessageType, Stopwatch Stopwatch);

/// <summary>
/// Middleware Wolverine de logging de latência.
///
/// Usa Envelope (tipo nativo Wolverine) em vez de T message (genérico aberto).
/// Parâmetros genéricos T em métodos de middleware causam UnResolvableVariableException
/// porque o JasperFx code gen não consegue fechar o tipo ao gerar o handler.
/// </summary>
public sealed class LoggingMiddleware
{
    public static LogEntry Before(Envelope envelope, ILogger<LoggingMiddleware> logger)
    {
        var typeName = envelope.Message?.GetType().Name ?? "Unknown";
        var entry = new LogEntry(typeName, Stopwatch.StartNew());

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Wolverine iniciando {MessageType}", entry.MessageType);

        return entry;
    }

    public static void After(ILogger<LoggingMiddleware> logger, LogEntry entry)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation(
                "Wolverine {MessageType} concluido em {ElapsedMs}ms",
                entry.MessageType,
                entry.Stopwatch.ElapsedMilliseconds);
    }
}
