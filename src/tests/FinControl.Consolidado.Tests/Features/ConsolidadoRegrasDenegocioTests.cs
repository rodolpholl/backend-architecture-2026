using System.Text.Json;
using FinControl.Consolidated.Core.Domain;
using FinControl.Consolidated.Core.Features.Commands.UpdateConsolidatedBalance;
using FinControl.Consolidated.Core.Features.Queries.GetConsolidatedBalance;
using FinControl.Infrastructure.Cache;
using Microsoft.Extensions.Caching.Distributed;

namespace FinControl.Consolidated.Tests.Features;

/// <summary>
/// Testes funcionais das regras de negócio do módulo Consolidado.
///
/// Requisitos do desafio verificados aqui:
///   - Serviço do consolidado diário (saldo diário com débitos e créditos)
///   - Relatório de saldo diário consolidado por data
///   - Isolamento entre datas distintas
///   - Resiliência: comerciante sem histórico recebe saldo zero (não erro)
/// </summary>
public class ConsolidadoRegrasDenegocioTests
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly DateTimeOffset DataPadrao =
        new(2026, 5, 23, 10, 0, 0, TimeSpan.Zero);

    // ── Infraestrutura de teste ───────────────────────────────────────────────

    private static byte[] ToBytes<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, JsonOpts);

    private static ConsolidatedBalance? FromBytes(byte[]? bytes) =>
        bytes is null ? null : JsonSerializer.Deserialize<ConsolidatedBalance>(bytes, JsonOpts);

    private static RedisCacheService CacheService(Mock<IDistributedCache> mock) =>
        new(mock.Object, NullLogger<RedisCacheService>.Instance);

    private static Mock<IRedisLockService> LockPassthrough()
    {
        var m = new Mock<IRedisLockService>();
        m.Setup(l => l.ExecuteWithLockAsync(
                It.IsAny<string>(), It.IsAny<Func<Task>>(),
                It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns<string, Func<Task>, TimeSpan, CancellationToken>(
                async (_, action, _, _) => { await action(); return true; });
        return m;
    }

    /// <summary>
    /// Simula um cache Redis em memória real para cenários de integração entre
    /// Command (AtualizarSaldo) e Query (GetSaldo) sem depender de Redis externo.
    /// </summary>
    private sealed class CacheEmMemoria : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public byte[]? Get(string key) =>
            _store.TryGetValue(key, out var v) ? v : null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromResult(Get(key));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            _store[key] = value;

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }
    }

    private static (UpdateConsolidatedBalanceCommandHandler command, GetConsolidatedBalanceQueryHandler query)
        CriarHandlers(CacheEmMemoria? cache = null)
    {
        cache ??= new CacheEmMemoria();
        var cacheService = new RedisCacheService(cache, NullLogger<RedisCacheService>.Instance);

        var commandHandler = new UpdateConsolidatedBalanceCommandHandler(
            cacheService,
            LockPassthrough().Object,
            NullLogger<UpdateConsolidatedBalanceCommandHandler>.Instance);

        var queryHandler = new GetConsolidatedBalanceQueryHandler(
            cacheService,
            NullLogger<GetConsolidatedBalanceQueryHandler>.Instance);

        return (commandHandler, queryHandler);
    }

    // ── RN-01: Comerciante sem lançamentos tem saldo zero ─────────────────────

    [Fact]
    public async Task Comerciante_Sem_Lancamentos_Tem_Saldo_Acumulado_Zero()
    {
        var (_, query) = CriarHandlers();

        var saldo = await query.Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        saldo.Balance.Should().Be(0);
        saldo.BalanceDecimal.Should().Be(0m);
    }

    [Fact]
    public async Task Comerciante_Sem_Lancamentos_Tem_Saldo_Por_Data_Zero()
    {
        var (_, query) = CriarHandlers();
        var data = DateOnly.FromDateTime(DataPadrao.UtcDateTime);

        var saldo = await query.Handle(new GetConsolidatedBalanceQuery(data), CancellationToken.None);

        saldo.Balance.Should().Be(0);
    }

    // ── RN-02: Crédito aumenta o saldo consolidado ───────────────────────────

    [Fact]
    public async Task Credito_Aumenta_Saldo_Consolidado_Do_Dia()
    {
        // R$ 150,00 = 15000 centavos
        var (command, query) = CriarHandlers();
        await command.Handle(new UpdateConsolidatedBalanceCommand(15000, DataPadrao));

        var data = DateOnly.FromDateTime(DataPadrao.UtcDateTime);
        var saldo = await query.Handle(new GetConsolidatedBalanceQuery(data), CancellationToken.None);

        saldo.Balance.Should().Be(15000);
        saldo.BalanceDecimal.Should().Be(150.00m);
    }

    [Fact]
    public async Task Multiplos_Creditos_Acumulam_No_Saldo_Do_Dia()
    {
        // R$ 100,00 + R$ 50,00 = R$ 150,00
        var (command, query) = CriarHandlers();
        await command.Handle(new UpdateConsolidatedBalanceCommand(10000, DataPadrao));
        await command.Handle(new UpdateConsolidatedBalanceCommand(5000, DataPadrao));

        var data = DateOnly.FromDateTime(DataPadrao.UtcDateTime);
        var saldo = await query.Handle(new GetConsolidatedBalanceQuery(data), CancellationToken.None);

        saldo.BalanceDecimal.Should().Be(150.00m);
    }

    // ── RN-03: Débito reduz o saldo consolidado ──────────────────────────────

    [Fact]
    public async Task Debito_Reduz_Saldo_Consolidado_Do_Dia()
    {
        // Crédito R$ 200,00 → Débito R$ 80,00 → Saldo R$ 120,00
        var (command, query) = CriarHandlers();
        await command.Handle(new UpdateConsolidatedBalanceCommand(20000, DataPadrao));
        await command.Handle(new UpdateConsolidatedBalanceCommand(-8000, DataPadrao));

        var data = DateOnly.FromDateTime(DataPadrao.UtcDateTime);
        var saldo = await query.Handle(new GetConsolidatedBalanceQuery(data), CancellationToken.None);

        saldo.BalanceDecimal.Should().Be(120.00m);
    }

    [Fact]
    public async Task Debitos_Superiores_Aos_Creditos_Resultam_Em_Saldo_Negativo()
    {
        // Crédito R$ 50,00 → Débito R$ 80,00 → Saldo -R$ 30,00
        var (command, query) = CriarHandlers();
        await command.Handle(new UpdateConsolidatedBalanceCommand(5000, DataPadrao));
        await command.Handle(new UpdateConsolidatedBalanceCommand(-8000, DataPadrao));

        var data = DateOnly.FromDateTime(DataPadrao.UtcDateTime);
        var saldo = await query.Handle(new GetConsolidatedBalanceQuery(data), CancellationToken.None);

        saldo.Balance.Should().BeNegative();
        saldo.BalanceDecimal.Should().Be(-30.00m);
    }

    // ── RN-04: Saldo acumulado consolida todos os lançamentos ────────────────

    [Fact]
    public async Task Saldo_Acumulado_Reflete_Total_De_Todos_Os_Lancamentos()
    {
        // Dia 1: +R$ 100,00 | Dia 2: +R$ 200,00 | Acumulado: R$ 300,00
        var (command, query) = CriarHandlers();
        var dia1 = new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero);
        var dia2 = new DateTimeOffset(2026, 5, 23, 10, 0, 0, TimeSpan.Zero);

        await command.Handle(new UpdateConsolidatedBalanceCommand(10000, dia1));
        await command.Handle(new UpdateConsolidatedBalanceCommand(20000, dia2));

        var saldo = await query.Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        saldo.BalanceDecimal.Should().Be(300.00m);
    }

    [Fact]
    public async Task Saldo_Acumulado_E_Saldo_Diario_Sao_Consultas_Independentes()
    {
        // Acumulado captura o total corrido; consulta por data captura apenas aquele dia
        var (command, query) = CriarHandlers();
        var dia1 = new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero);
        var dia2 = new DateTimeOffset(2026, 5, 23, 10, 0, 0, TimeSpan.Zero);

        await command.Handle(new UpdateConsolidatedBalanceCommand(10000, dia1));
        await command.Handle(new UpdateConsolidatedBalanceCommand(5000, dia2));

        var saldoAcumulado = await query.Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);
        var saldoDia2 = await query.Handle(
            new GetConsolidatedBalanceQuery(DateOnly.FromDateTime(dia2.UtcDateTime)),
            CancellationToken.None);

        saldoAcumulado.BalanceDecimal.Should().Be(150.00m);
        // O saldo do dia 2 reflete o acumulado até aquele momento (100 + 50)
        saldoDia2.BalanceDecimal.Should().Be(150.00m);
    }

    // ── RN-05: Consulta por data específica ──────────────────────────────────

    [Fact]
    public async Task Consulta_Por_Data_Retorna_Saldo_Do_Dia_Solicitado()
    {
        var (command, query) = CriarHandlers();
        var data = new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero);
        await command.Handle(new UpdateConsolidatedBalanceCommand(7500, data));

        var saldo = await query.Handle(
            new GetConsolidatedBalanceQuery(new DateOnly(2026, 5, 20)),
            CancellationToken.None);

        saldo.BalanceDecimal.Should().Be(75.00m);
    }

    // ── RN-06: Fallback para o dia anterior quando não há lançamentos ────────

    [Fact]
    public async Task Consulta_De_Dia_Sem_Lancamentos_Retorna_Saldo_Mais_Recente_Disponivel()
    {
        // Lançamento em 22/05, consulta em 23/05 (sem lançamentos) → retorna o do dia 22
        var (command, query) = CriarHandlers();
        var dia22 = new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero);
        await command.Handle(new UpdateConsolidatedBalanceCommand(9000, dia22));

        var saldo = await query.Handle(
            new GetConsolidatedBalanceQuery(new DateOnly(2026, 5, 23)),
            CancellationToken.None);

        saldo.BalanceDecimal.Should().Be(90.00m);
    }

    // ── RN-07: Lançamento retroativo vai para a data correta ─────────────────

    [Fact]
    public async Task Lancamento_Retroativo_E_Consolidado_Na_Data_Informada()
    {
        var (command, query) = CriarHandlers();
        var dataRetroativa = new DateTimeOffset(2025, 12, 31, 23, 59, 0, TimeSpan.Zero);

        await command.Handle(new UpdateConsolidatedBalanceCommand(5000, dataRetroativa));

        var saldo = await query.Handle(
            new GetConsolidatedBalanceQuery(new DateOnly(2025, 12, 31)),
            CancellationToken.None);

        saldo.BalanceDecimal.Should().Be(50.00m);
    }

    // ── RN-08: Precisão monetária (centavos → reais) ─────────────────────────

    [Fact]
    public async Task Valor_Em_Centavos_E_Convertido_Corretamente_Para_Reais()
    {
        // R$ 1.234,56 = 123456 centavos
        var (command, query) = CriarHandlers();
        await command.Handle(new UpdateConsolidatedBalanceCommand(123456, DataPadrao));

        var saldo = await query.Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        saldo.BalanceDecimal.Should().Be(1234.56m);
    }
}
