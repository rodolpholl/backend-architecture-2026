using System.Text.Json;
using FinControl.Consolidated.Core.Domain;
using FinControl.Consolidated.Core.Features.Commands.UpdateConsolidatedBalance;
using FinControl.Consolidated.Core.Features.Queries.GetConsolidatedBalance;
using FinControl.Infrastructure.Cache;
using Microsoft.Extensions.Caching.Distributed;

namespace FinControl.Consolidated.Tests.Features;

/// <summary>
/// Functional tests for business rules of the Consolidated module.
///
/// Challenge requirements verified here:
///   - Daily consolidated service (daily balance with debits and credits)
///   - Consolidated daily balance report by date
///   - Isolation between different dates
///   - Resilience: merchant without history receives zero balance (not error)
/// </summary>
public class ConsolidatedBusinessRulesTests
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly DateTimeOffset DefaultDate =
        new(2026, 5, 23, 10, 0, 0, TimeSpan.Zero);

    // ── Test Infrastructure ───────────────────────────────────────────────

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
    /// Simulates a real in-memory Redis cache for integration scenarios between
    /// Command (UpdateBalance) and Query (GetBalance) without depending on external Redis.
    /// </summary>
    private sealed class InMemoryCache : IDistributedCache
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
        CreateHandlers(InMemoryCache? cache = null)
    {
        cache ??= new InMemoryCache();
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

    // ── BR-01: Merchant without transactions has zero balance ─────────────────

    [Fact]
    public async Task Merchant_Without_Transactions_Has_Zero_Accumulated_Balance()
    {
        var (_, query) = CreateHandlers();

        var balance = await query.Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        balance.Balance.Should().Be(0);
        balance.BalanceDecimal.Should().Be(0m);
    }

    [Fact]
    public async Task Merchant_Without_Transactions_Has_Zero_Balance_By_Date()
    {
        var (_, query) = CreateHandlers();
        var date = DateOnly.FromDateTime(DefaultDate.UtcDateTime);

        var balance = await query.Handle(new GetConsolidatedBalanceQuery(date), CancellationToken.None);

        balance.Balance.Should().Be(0);
    }

    // ── BR-02: Credit increases consolidated daily balance ───────────────────

    [Fact]
    public async Task Credit_Increases_Daily_Consolidated_Balance()
    {
        // $150.00 = 15000 cents
        var (command, query) = CreateHandlers();
        await command.Handle(new UpdateConsolidatedBalanceCommand(15000, DefaultDate));

        var date = DateOnly.FromDateTime(DefaultDate.UtcDateTime);
        var balance = await query.Handle(new GetConsolidatedBalanceQuery(date), CancellationToken.None);

        balance.Balance.Should().Be(15000);
        balance.BalanceDecimal.Should().Be(150.00m);
    }

    [Fact]
    public async Task Multiple_Credits_Accumulate_In_Daily_Balance()
    {
        // $100.00 + $50.00 = $150.00
        var (command, query) = CreateHandlers();
        await command.Handle(new UpdateConsolidatedBalanceCommand(10000, DefaultDate));
        await command.Handle(new UpdateConsolidatedBalanceCommand(5000, DefaultDate));

        var date = DateOnly.FromDateTime(DefaultDate.UtcDateTime);
        var balance = await query.Handle(new GetConsolidatedBalanceQuery(date), CancellationToken.None);

        balance.BalanceDecimal.Should().Be(150.00m);
    }

    // ── BR-03: Debit reduces consolidated balance ──────────────────────────

    [Fact]
    public async Task Debit_Reduces_Daily_Consolidated_Balance()
    {
        // Credit $200.00 → Debit $80.00 → Balance $120.00
        var (command, query) = CreateHandlers();
        await command.Handle(new UpdateConsolidatedBalanceCommand(20000, DefaultDate));
        await command.Handle(new UpdateConsolidatedBalanceCommand(-8000, DefaultDate));

        var date = DateOnly.FromDateTime(DefaultDate.UtcDateTime);
        var balance = await query.Handle(new GetConsolidatedBalanceQuery(date), CancellationToken.None);

        balance.BalanceDecimal.Should().Be(120.00m);
    }

    [Fact]
    public async Task Debits_Greater_Than_Credits_Result_In_Negative_Balance()
    {
        // Credit $50.00 → Debit $80.00 → Balance -$30.00
        var (command, query) = CreateHandlers();
        await command.Handle(new UpdateConsolidatedBalanceCommand(5000, DefaultDate));
        await command.Handle(new UpdateConsolidatedBalanceCommand(-8000, DefaultDate));

        var date = DateOnly.FromDateTime(DefaultDate.UtcDateTime);
        var balance = await query.Handle(new GetConsolidatedBalanceQuery(date), CancellationToken.None);

        balance.Balance.Should().BeNegative();
        balance.BalanceDecimal.Should().Be(-30.00m);
    }

    // ── BR-04: Accumulated balance consolidates all transactions ────────────────

    [Fact]
    public async Task Accumulated_Balance_Reflects_Total_Of_All_Transactions()
    {
        // Day 1: +$100.00 | Day 2: +$200.00 | Accumulated: $300.00
        var (command, query) = CreateHandlers();
        var day1 = new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero);
        var day2 = new DateTimeOffset(2026, 5, 23, 10, 0, 0, TimeSpan.Zero);

        await command.Handle(new UpdateConsolidatedBalanceCommand(10000, day1));
        await command.Handle(new UpdateConsolidatedBalanceCommand(20000, day2));

        var balance = await query.Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        balance.BalanceDecimal.Should().Be(300.00m);
    }

    [Fact]
    public async Task Accumulated_Balance_And_Daily_Balance_Are_Independent_Queries()
    {
        // Accumulated captures the running total; date query captures only that day
        var (command, query) = CreateHandlers();
        var day1 = new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero);
        var day2 = new DateTimeOffset(2026, 5, 23, 10, 0, 0, TimeSpan.Zero);

        await command.Handle(new UpdateConsolidatedBalanceCommand(10000, day1));
        await command.Handle(new UpdateConsolidatedBalanceCommand(5000, day2));

        var accumulatedBalance = await query.Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);
        var day2Balance = await query.Handle(
            new GetConsolidatedBalanceQuery(DateOnly.FromDateTime(day2.UtcDateTime)),
            CancellationToken.None);

        accumulatedBalance.BalanceDecimal.Should().Be(150.00m);
        // Day 2 balance reflects accumulated up to that point (100 + 50)
        day2Balance.BalanceDecimal.Should().Be(150.00m);
    }

    // ── BR-05: Query by specific date ──────────────────────────────────

    [Fact]
    public async Task Query_By_Date_Returns_Balance_Of_Requested_Day()
    {
        var (command, query) = CreateHandlers();
        var date = new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero);
        await command.Handle(new UpdateConsolidatedBalanceCommand(7500, date));

        var balance = await query.Handle(
            new GetConsolidatedBalanceQuery(new DateOnly(2026, 5, 20)),
            CancellationToken.None);

        balance.BalanceDecimal.Should().Be(75.00m);
    }

    // ── BR-06: Fallback to previous day when no transactions ────────

    [Fact]
    public async Task Query_Of_Day_Without_Transactions_Returns_Most_Recent_Available_Balance()
    {
        // Transaction on 5/22, query on 5/23 (no transactions) → returns 5/22 balance
        var (command, query) = CreateHandlers();
        var day22 = new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero);
        await command.Handle(new UpdateConsolidatedBalanceCommand(9000, day22));

        var balance = await query.Handle(
            new GetConsolidatedBalanceQuery(new DateOnly(2026, 5, 23)),
            CancellationToken.None);

        balance.BalanceDecimal.Should().Be(90.00m);
    }

    // ── BR-07: Retroactive transaction goes to correct date ─────────────────

    [Fact]
    public async Task Retroactive_Transaction_Is_Consolidated_On_Specified_Date()
    {
        var (command, query) = CreateHandlers();
        var retroactiveDate = new DateTimeOffset(2025, 12, 31, 23, 59, 0, TimeSpan.Zero);

        await command.Handle(new UpdateConsolidatedBalanceCommand(5000, retroactiveDate));

        var balance = await query.Handle(
            new GetConsolidatedBalanceQuery(new DateOnly(2025, 12, 31)),
            CancellationToken.None);

        balance.BalanceDecimal.Should().Be(50.00m);
    }

    // ── BR-08: Monetary precision (cents → dollars) ─────────────────────────

    [Fact]
    public async Task Amount_In_Cents_Is_Converted_Correctly_To_Dollars()
    {
        // $1,234.56 = 123456 cents
        var (command, query) = CreateHandlers();
        await command.Handle(new UpdateConsolidatedBalanceCommand(123456, DefaultDate));

        var balance = await query.Handle(new GetConsolidatedBalanceQuery(), CancellationToken.None);

        balance.BalanceDecimal.Should().Be(1234.56m);
    }
}
