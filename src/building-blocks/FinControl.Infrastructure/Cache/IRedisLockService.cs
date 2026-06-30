namespace FinControl.Infrastructure.Cache;

/// <summary>
/// Executa um bloco de código com exclusão mútua distribuída via Redis.
/// Garante que apenas uma instância por chave execute o bloco ao mesmo tempo,
/// eliminando race conditions em operações READ-MODIFY-WRITE no cache.
/// </summary>
public interface IRedisLockService
{
    /// <summary>
    /// Adquire o lock para <paramref name="lockKey"/>, executa <paramref name="action"/>
    /// e libera o lock no bloco finally — mesmo em caso de exceção.
    /// </summary>
    /// <returns>
    /// <c>true</c> se o lock foi adquirido e a ação executada;
    /// <c>false</c> se o lock não pôde ser adquirido após as tentativas (outro nó detém o lock).
    /// </returns>
    Task<bool> ExecuteWithLockAsync(
        string lockKey,
        Func<Task> action,
        TimeSpan lockExpiry,
        CancellationToken ct = default);
}
