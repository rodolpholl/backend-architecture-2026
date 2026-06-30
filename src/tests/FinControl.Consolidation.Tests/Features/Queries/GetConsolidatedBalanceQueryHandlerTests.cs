using System.Text.Json;
using FinControl.Consolidation.Core.Domain;
using FinControl.Consolidation.Core.Features.Queries.GetConsolidatedBalance;
using FinControl.Consolidation.Tests.Fakers;
using FinControl.Infrastructure.Cache;
using Microsoft.Extensions.Caching.Distributed;

namespace FinControl.Consolidation.Tests.Features.Queries;

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

    // Mock that returns data only for the specified key; null for everything else
    private static Mock<IDistributedCache> MockWithKey(string key, ConsolidatedBalance balance)
    {
        var m = new Mock<IDistributedCache>();
        m.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((cacheKey, _) =>
                cacheKey == key
                    ? Task.FromResult<byte[]?>(ToBytes(balance))
                    : Task.FromResult<byte[]?>(null));
        return m;
    }

    private static Mock<IDistributedCache> MockEmpty()
    {
        var m = new Mock<IDistributedCache>();
        m.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        m.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m;
    }

    // ── Accumulated balance (no date) ────────────────────────────────────────────

    [Fact]
    public async Task Should_Return_Accumulated_Balance_When_Cache_Contains_Data()
    {
        var balance = ConsolidatedBalanceFaker.Positivo(15000);
        var cacheMock = MockWithKey("balance:consolidated:accumulated", balance);

        var response = await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        response.Balance.Should().Be(15000);
    }

    [Fact]
    public async Task Should_Query_Accumulated_Key_When_Date_Not_Provided()
    {
        var cacheMock = MockEmpty();

        await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        cacheMock.Verify(c => c.GetAsync(
            "balance:consolidated:accumulated",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Should_Return_Zero_When_Accumulated_Balance_Not_In_Cache()
    {
        var response = await CreateHandler(MockEmpty())
            .Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        response.Balance.Should().Be(0);
        response.BalanceDecimal.Should().Be(0m);
    }

    // ── Balance by specific date — cache hit ─────────────────────────────────

    [Fact]
    public async Task Should_Return_Balance_Of_Date_When_Exists_In_Cache()
    {
        var date = new DateOnly(2026, 5, 23);
        var balance = ConsolidatedBalanceFaker.Positivo(8500);
        var cacheMock = MockWithKey("balance:consolidated:2026-05-23", balance);

        var response = await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(date), CancellationToken.None);

        response.Balance.Should().Be(8500);
    }

    [Fact]
    public async Task Should_Query_Cache_With_Specific_Formatted_Date()
    {
        var date = new DateOnly(2026, 5, 23);
        var cacheMock = MockEmpty();

        await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(date), CancellationToken.None);

        cacheMock.Verify(c => c.GetAsync(
            "balance:consolidated:2026-05-23",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Fallback — search previous days ──────────────────────────────────────

    [Fact]
    public async Task Should_Search_Previous_Day_When_Date_Not_Found_In_Cache()
    {
        var date = new DateOnly(2026, 5, 23);
        var balanceYesterday = ConsolidatedBalanceFaker.Positivo(5000);
        var cacheMock = MockWithKey("balance:consolidated:2026-05-22", balanceYesterday);

        var response = await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(date), CancellationToken.None);

        response.Balance.Should().Be(5000);
    }

    [Fact]
    public async Task Should_Return_Balance_Of_First_Previous_Day_With_Data()
    {
        // Data available 3 days ago, but not 1 or 2 days ago
        var date = new DateOnly(2026, 5, 23);
        var balance3DaysAgo = ConsolidatedBalanceFaker.Positivo(12000);
        var cacheMock = MockWithKey("balance:consolidated:2026-05-20", balance3DaysAgo);

        var response = await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(date), CancellationToken.None);

        response.Balance.Should().Be(12000);
    }

    [Fact]
    public async Task Should_Propagate_Found_Balance_To_Requested_Date()
    {
        var date = new DateOnly(2026, 5, 23);
        var balanceYesterday = ConsolidatedBalanceFaker.Positivo(7000);
        var cacheMock = MockWithKey("balance:consolidated:2026-05-22", balanceYesterday);

        cacheMock.Setup(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(date), CancellationToken.None);

        // Balance found yesterday should be propagated to requested date key
        cacheMock.Verify(c => c.SetAsync(
            "balance:consolidated:2026-05-23",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Should_Propagate_With_TTL_Of_30_Days()
    {
        var date = new DateOnly(2026, 5, 23);
        var balanceYesterday = ConsolidatedBalanceFaker.Positivo(7000);
        var cacheMock = MockWithKey("balance:consolidated:2026-05-22", balanceYesterday);

        DistributedCacheEntryOptions? capturedOpts = null;
        cacheMock.Setup(c => c.SetAsync(
            "balance:consolidated:2026-05-23", It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (_, _, opts, _) => capturedOpts = opts)
            .Returns(Task.CompletedTask);

        await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(date), CancellationToken.None);

        capturedOpts!.AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public async Task Should_Try_Maximum_30_Days_Previous_In_Fallback()
    {
        // All 31 GetAsync calls (1 for the date + 30 fallback) should return null
        var date = new DateOnly(2026, 5, 23);
        var cacheMock = MockEmpty();

        await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(date), CancellationToken.None);

        // 1 call for requested date + 30 fallback attempts = 31 total
        cacheMock.Verify(c => c.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(31));
    }

    [Fact]
    public async Task Should_Stop_Fallback_On_First_Day_With_Balance()
    {
        // Data available 1 day ago — should stop immediately, without trying remaining 29 days
        var date = new DateOnly(2026, 5, 23);
        var balanceYesterday = ConsolidatedBalanceFaker.Positivo(3000);
        var cacheMock = MockWithKey("balance:consolidated:2026-05-22", balanceYesterday);

        cacheMock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(date), CancellationToken.None);

        // 1 for requested date + 1 for yesterday (hit) = 2 calls total
        cacheMock.Verify(c => c.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Should_Return_Zero_Balance_When_No_Data_In_Last_30_Days()
    {
        var date = new DateOnly(2026, 5, 23);

        var response = await CreateHandler(MockEmpty())
            .Handle(new GetConsolidatedBalanceQuery(date), CancellationToken.None);

        response.Balance.Should().Be(0);
        response.BalanceDecimal.Should().Be(0m);
    }

    // ── Unit Conversion ──────────────────────────────────────────────────

    [Fact]
    public async Task Should_Convert_Cents_To_Decimal_Correctly()
    {
        var balance = new ConsolidatedBalance(15550, DateTimeOffset.UtcNow);
        var cacheMock = MockWithKey("balance:consolidated:accumulated", balance);

        var response = await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        response.BalanceDecimal.Should().Be(155.50m);
    }

    [Fact]
    public async Task Should_Return_Negative_Balance_When_Debits_Exceed_Credits()
    {
        var balance = ConsolidatedBalanceFaker.Negativo(-5000);
        var cacheMock = MockWithKey("balance:consolidated:accumulated", balance);

        var response = await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        response.Balance.Should().BeNegative();
        response.BalanceDecimal.Should().BeNegative();
    }

    [Fact]
    public async Task Should_Return_LastUpdated_From_Cache()
    {
        var when = DateTimeOffset.UtcNow.AddMinutes(-10);
        var balance = new ConsolidatedBalance(1000, when);
        var cacheMock = MockWithKey("balance:consolidated:accumulated", balance);

        var response = await CreateHandler(cacheMock)
            .Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        response.LastUpdated.Should().BeCloseTo(when, TimeSpan.FromSeconds(1));
    }
}

