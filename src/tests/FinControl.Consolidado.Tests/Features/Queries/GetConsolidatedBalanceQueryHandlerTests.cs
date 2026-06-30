using System.Text.Json;
using FinControl.Consolidated.Core.Domain;
using FinControl.Consolidated.Core.Features.Queries.GetConsolidatedBalance;
using FinControl.Consolidated.Tests.Fakers;
using FinControl.Infrastructure.Cache;
using Microsoft.Extensions.Caching.Distributed;

namespace FinControl.Consolidated.Tests.Features.Queries;

public class GetConsolidatedBalanceQueryHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static byte[] ToBytes<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, JsonOpts);

    private static RedisCacheService CacheService(Mock<IDistributedCache> mock) =>
        new(mock.Object, NullLogger<RedisCacheService>.Instance);

    private static GetConsolidatedBalanceQueryHandler CreateHandler(Mock<IDistributedCache> cacheMock) =>
        new(CacheService(cacheMock), NullLogger<GetConsolidatedBalanceQueryHandler>.Instance);

    // Mock que retorna dados apenas para a chave informada; null para todo o resto
    private static Mock<IDistributedCache> MockComChave(string chave, ConsolidatedBalance saldo)
    {
        var m = new Mock<IDistributedCache>();
        m.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((key, _) =>
                key == chave
                    ? Task.FromResult<byte[]?>(ToBytes(saldo))
                    : Task.FromResult<byte[]?>(null));
        return m;
    }

    private static Mock<IDistributedCache> MockVazio()
    {
        var m = new Mock<IDistributedCache>();
        m.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        m.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m;
    }

    // ── Saldo acumulado (sem data) ────────────────────────────────────────────

    [Fact]
    public async Task Deve_Retornar_Saldo_Acumulado_Quando_Cache_Contem_Dado()
    {
        var saldo = ConsolidatedBalanceFaker.Positivo(15000);
        var cacheMock = MockComChave("saldo:consolidado:acumulado", saldo);

        var response = await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        response.Balance.Should().Be(15000);
    }

    [Fact]
    public async Task Deve_Consultar_Chave_Acumulada_Quando_Data_Nao_Informada()
    {
        var cacheMock = MockVazio();

        await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        cacheMock.Verify(c => c.GetAsync(
            "saldo:consolidado:acumulado",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deve_Retornar_Zero_Quando_Saldo_Acumulado_Nao_Esta_No_Cache()
    {
        var response = await CreateHandler(MockVazio())
            .Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        response.Balance.Should().Be(0);
        response.BalanceDecimal.Should().Be(0m);
    }

    // ── Saldo por data específica — cache hit ─────────────────────────────────

    [Fact]
    public async Task Deve_Retornar_Saldo_Da_Data_Quando_Existe_No_Cache()
    {
        var data = new DateOnly(2026, 5, 23);
        var saldo = ConsolidatedBalanceFaker.Positivo(8500);
        var cacheMock = MockComChave("saldo:consolidado:2026-05-23", saldo);

        var response = await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(data), CancellationToken.None);

        response.Balance.Should().Be(8500);
    }

    [Fact]
    public async Task Deve_Consultar_Cache_Com_Data_Especifica_Formatada()
    {
        var data = new DateOnly(2026, 5, 23);
        var cacheMock = MockVazio();

        await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(data), CancellationToken.None);

        cacheMock.Verify(c => c.GetAsync(
            "saldo:consolidado:2026-05-23",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Fallback — busca dias anteriores ──────────────────────────────────────

    [Fact]
    public async Task Deve_Buscar_Dia_Anterior_Quando_Data_Nao_Encontrada_No_Cache()
    {
        var data = new DateOnly(2026, 5, 23);
        var saldoOntem = ConsolidatedBalanceFaker.Positivo(5000);
        var cacheMock = MockComChave("saldo:consolidado:2026-05-22", saldoOntem);

        var response = await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(data), CancellationToken.None);

        response.Balance.Should().Be(5000);
    }

    [Fact]
    public async Task Deve_Retornar_Saldo_Do_Primeiro_Dia_Anterior_Com_Dado()
    {
        // Há dados 3 dias atrás, mas não 1 ou 2 dias atrás
        var data = new DateOnly(2026, 5, 23);
        var saldo3DiasAtras = ConsolidatedBalanceFaker.Positivo(12000);
        var cacheMock = MockComChave("saldo:consolidado:2026-05-20", saldo3DiasAtras);

        var response = await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(data), CancellationToken.None);

        response.Balance.Should().Be(12000);
    }

    [Fact]
    public async Task Deve_Propagar_Saldo_Encontrado_Para_Data_Requisitada()
    {
        var data = new DateOnly(2026, 5, 23);
        var saldoOntem = ConsolidatedBalanceFaker.Positivo(7000);
        var cacheMock = MockComChave("saldo:consolidado:2026-05-22", saldoOntem);

        cacheMock.Setup(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(data), CancellationToken.None);

        // O saldo encontrado ontem deve ser propagado para a chave da data requisitada
        cacheMock.Verify(c => c.SetAsync(
            "saldo:consolidado:2026-05-23",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deve_Propagar_Com_TTL_De_30_Dias()
    {
        var data = new DateOnly(2026, 5, 23);
        var saldoOntem = ConsolidatedBalanceFaker.Positivo(7000);
        var cacheMock = MockComChave("saldo:consolidado:2026-05-22", saldoOntem);

        DistributedCacheEntryOptions? capturedOpts = null;
        cacheMock.Setup(c => c.SetAsync(
            "saldo:consolidado:2026-05-23", It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (_, _, opts, _) => capturedOpts = opts)
            .Returns(Task.CompletedTask);

        await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(data), CancellationToken.None);

        capturedOpts!.AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public async Task Deve_Tentar_No_Maximo_30_Dias_Anteriores_No_Fallback()
    {
        // Todos os 31 GetAsync (1 para a data + 30 fallback) devem retornar null
        var data = new DateOnly(2026, 5, 23);
        var cacheMock = MockVazio();

        await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(data), CancellationToken.None);

        // 1 chamada para a data requisitada + 30 tentativas de fallback = 31 total
        cacheMock.Verify(c => c.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(31));
    }

    [Fact]
    public async Task Deve_Parar_Fallback_No_Primeiro_Dia_Com_Saldo()
    {
        // Dados disponíveis 1 dia atrás — deve parar imediatamente, sem tentar os 29 dias restantes
        var data = new DateOnly(2026, 5, 23);
        var saldoOntem = ConsolidatedBalanceFaker.Positivo(3000);
        var cacheMock = MockComChave("saldo:consolidado:2026-05-22", saldoOntem);

        cacheMock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(data), CancellationToken.None);

        // 1 para a data requisitada + 1 para ontem (hit) = 2 chamadas no total
        cacheMock.Verify(c => c.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Deve_Retornar_Saldo_Zero_Quando_Nenhum_Dado_Nos_Ultimos_30_Dias()
    {
        var data = new DateOnly(2026, 5, 23);

        var response = await CreateHandler(MockVazio())
            .Handle(new GetConsolidatedBalanceQuery(data), CancellationToken.None);

        response.Balance.Should().Be(0);
        response.BalanceDecimal.Should().Be(0m);
    }

    // ── Conversão de unidade ──────────────────────────────────────────────────

    [Fact]
    public async Task Deve_Converter_Centavos_Para_Decimal_Corretamente()
    {
        var saldo = new ConsolidatedBalance(15550, DateTimeOffset.UtcNow);
        var cacheMock = MockComChave("saldo:consolidado:acumulado", saldo);

        var response = await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        response.BalanceDecimal.Should().Be(155.50m);
    }

    [Fact]
    public async Task Deve_Retornar_Saldo_Negativo_Quando_Debitos_Superam_Creditos()
    {
        var saldo = ConsolidatedBalanceFaker.Negativo(-5000);
        var cacheMock = MockComChave("saldo:consolidado:acumulado", saldo);

        var response = await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        response.Balance.Should().BeNegative();
        response.BalanceDecimal.Should().BeNegative();
    }

    [Fact]
    public async Task Deve_Retornar_UltimaAtualizacao_Do_Cache()
    {
        var quando = DateTimeOffset.UtcNow.AddMinutes(-10);
        var saldo = new ConsolidatedBalance(1000, quando);
        var cacheMock = MockComChave("saldo:consolidado:acumulado", saldo);

        var response = await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        response.LastUpdated.Should().BeCloseTo(quando, TimeSpan.FromSeconds(1));
    }
}
