using System.Text.Json.Serialization;

namespace FinControl.StressTests.Fakers;

// DTO espelho de RegisterTransactionCommand (sem referência ao projeto Core).
// Propriedades em camelCase via JsonPropertyName para garantir serialização correta.
internal sealed record LancamentoRequest(
    [property: JsonPropertyName("modalidade")]     int             Category,
    [property: JsonPropertyName("valor")]          long            Amount,
    [property: JsonPropertyName("descricao")]      string?         Description,
    [property: JsonPropertyName("transactionDate")] DateTimeOffset  TransactionDate,
    [property: JsonPropertyName("usuarioId")]      string          UserId,
    [property: JsonPropertyName("usuarioNome")]    string          UserName,
    [property: JsonPropertyName("usuarioEmail")]   string          UserEmail,
    [property: JsonPropertyName("idempotencyKey")] Guid            IdempotencyKey,
    [property: JsonPropertyName("correlationId")]  Guid            CorrelationId);
