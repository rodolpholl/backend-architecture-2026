using System.Text.Json.Serialization;

namespace FinControl.StressTests.Fakers;

// DTO mirror of RegisterTransactionCommand (without reference to Core project).
// Properties in camelCase via JsonPropertyName to ensure correct serialization.
internal sealed record TransactionRequest(
    [property: JsonPropertyName("modalidade")]     int             Category,
    [property: JsonPropertyName("valor")]          long            Amount,
    [property: JsonPropertyName("descricao")]      string?         Description,
    [property: JsonPropertyName("transactionDate")] DateTimeOffset  TransactionDate,
    [property: JsonPropertyName("usuarioId")]      string          UserId,
    [property: JsonPropertyName("usuarioNome")]    string          UserName,
    [property: JsonPropertyName("usuarioEmail")]   string          UserEmail,
    [property: JsonPropertyName("idempotencyKey")] Guid            IdempotencyKey,
    [property: JsonPropertyName("correlationId")]  Guid            CorrelationId);
