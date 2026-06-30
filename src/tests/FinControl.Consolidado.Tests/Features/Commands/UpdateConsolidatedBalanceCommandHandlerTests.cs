using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using FinControl.Consolidated.Core.Domain;
using FinControl.Consolidated.Core.Features.Commands.UpdateConsolidatedBalance;
using FinControl.Consolidated.Tests.Fakers;
using FinControl.Infrastructure.Cache;

namespace FinControl.Consolidated.Tests.Features.Commands;

public class UpdateConsolidatedBalanceCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly DateTimeOffset DefaultDate =
        new(2026, 5, 23, 10, 0, 0, TimeSpan.Zero);

    private static RedisCacheService CacheService(Mock<IDistributedCache> mock) =>
        new(mock.Object, NullLogger<RedisCacheService>.Instance);

    private static byte[] ToBytes<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, JsonOpts);

    private static ConsolidatedBalance? FromBytes(byte[]? bytes) =>
        bytes is null ? null : JsonSerializer.Deserialize<ConsolidatedBalance>(bytes, JsonOpts);

    // IRedisLockService that executes action immediately (no real Redis in tests)
    private static Mock<IRedisLockService> LockMockPassthrough()
    {
        var m = new Mock<IRedisLockService>();
        m.Setup(l => l.ExecuteWithLockAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<Task>, TimeSpan, CancellationToken>(
                async (_, action, _, _) => { await action(); return true; });
        return m;
    }

    private static (UpdateConsolidatedBalanceCommandHandler handler, Mock<IDistributedCache> cacheMock)
        CreateHandler(ConsolidatedBalance? currentBalance = null)
    {
        var cacheMock = new Mock<IDistributedCache>();
        cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(currentBalance is null ? null : ToBytes(currentBalance));

        var handler = new UpdateConsolidatedBalanceCommandHandler(
            CacheService(cacheMock),
            LockMockPassthrough().Object,
            NullLogger<UpdateConsolidatedBalanceCommandHandler>.Instance);

        return (handler, cacheMock);
    }

    private static UpdateConsolidatedBalanceCommand Cmd(long amount) =>
        new(TransactionAmount: amount, TransactionDate: DefaultDate);

    private static byte[]? CaptureSavedBytes(Mock<IDistributedCache> cacheMock)
    {
        byte[]? saved = null;
        cacheMock.Setup(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (_, bytes, _, _) => saved = bytes)
            .Returns(Task.CompletedTask);
        return saved;
    }

    // ── Balance Creation ─────────────────────────────────────────────────────

    [Fact]
    public async Task Should_Create_Initial_Balance_When_Cache_Empty()
    {
        var (handler, cacheMock) = CreateHandler(currentBalance: null);

        byte[]? savedBytes = null;
        cacheMock.Setup(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (_, b, _, _) => savedBytes = b)
            .Returns(Task.CompletedTask);

        await handler.Handle(Cmd(5000));

        var saved = FromBytes(savedBytes);
        saved.Should().NotBeNull();
        saved!.Balance.Should().Be(5000);
    }

    // ── Accumulation ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_Accumulate_Over_Existing_Balance()
    {
        var (handler, cacheMock) = CreateHandler(ConsolidatedBalanceFaker.Positivo(10000));

        byte[]? savedBytes = null;
        cacheMock.Setup(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (_, b, _, _) => savedBytes = b)
            .Returns(Task.CompletedTask);

        await handler.Handle(Cmd(3000));

        FromBytes(savedBytes)!.Balance.Should().Be(13000);
    }

    [Fact]
    public async Task Should_Decrement_Balance_For_Negative_Transaction()
    {
        var (handler, cacheMock) = CreateHandler(ConsolidatedBalanceFaker.Positivo(10000));

        byte[]? savedBytes = null;
        cacheMock.Setup(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (_, b, _, _) => savedBytes = b)
            .Returns(Task.CompletedTask);

        await handler.Handle(Cmd(-4000));

        FromBytes(savedBytes)!.Balance.Should().Be(6000);
    }

    [Fact]
    public async Task Balance_Can_Go_Negative_When_Debits_Exceed_Credits()
    {
        var (handler, cacheMock) = CreateHandler(ConsolidatedBalanceFaker.Positivo(1000));

        byte[]? savedBytes = null;
        cacheMock.Setup(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (_, b, _, _) => savedBytes = b)
            .Returns(Task.CompletedTask);

        await handler.Handle(Cmd(-5000));

        FromBytes(savedBytes)!.Balance.Should().Be(-4000);
    }

    // ── TTL ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_Save_With_TTL_Of_30_Days()
    {
        var (handler, cacheMock) = CreateHandler();

        DistributedCacheEntryOptions? capturedOpts = null;
        cacheMock.Setup(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (_, _, opts, _) => capturedOpts = opts)
            .Returns(Task.CompletedTask);

        await handler.Handle(Cmd(1000));

        capturedOpts.Should().NotBeNull();
        capturedOpts!.AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromDays(30));
    }

    // ── Cache key uses transaction date (not UtcNow) ───────────────────────────

    [Fact]
    public async Task Should_Use_Key_With_Transaction_Date()
    {
        var transactionDate = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var expectedKey = "balance:consolidated:2026-01-15";

        var (handler, cacheMock) = CreateHandler();
        cacheMock.Setup(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await handler.Handle(new UpdateConsolidatedBalanceCommand(500, transactionDate));

        cacheMock.Verify(c => c.SetAsync(
            expectedKey,
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Retroactive transaction goes to correct date ──────────────────────────

    [Fact]
    public async Task Should_Use_Transaction_Date_For_Retroactive_Entry()
    {
        var retroactiveDate = new DateTimeOffset(2025, 12, 31, 23, 59, 0, TimeSpan.Zero);
        var expectedKey = "balance:consolidated:2025-12-31";

        var (handler, cacheMock) = CreateHandler();
        cacheMock.Setup(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await handler.Handle(new UpdateConsolidatedBalanceCommand(1000, retroactiveDate));

        cacheMock.Verify(c => c.SetAsync(
            expectedKey,
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── LastUpdated ────────────────────────────────────────────────────

    [Fact]
    public async Task Should_Update_LastUpdated_To_Now()
    {
        var (handler, cacheMock) = CreateHandler(ConsolidatedBalanceFaker.Positivo(1000));

        byte[]? savedBytes = null;
        cacheMock.Setup(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (_, b, _, _) => savedBytes = b)
            .Returns(Task.CompletedTask);

        var before = DateTimeOffset.UtcNow;
        await handler.Handle(Cmd(500));

        FromBytes(savedBytes)!.LastUpdated.Should().BeOnOrAfter(before);
    }

    // ── Distributed lock is invoked ───────────────────────────────────────────

    [Fact]
    public async Task Should_Invoke_Distributed_Lock_For_Each_Update()
    {
        var (_, cacheMock) = CreateHandler();
        var lockMock = LockMockPassthrough();

        cacheMock.Setup(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new UpdateConsolidatedBalanceCommandHandler(
            CacheService(cacheMock),
            lockMock.Object,
            NullLogger<UpdateConsolidatedBalanceCommandHandler>.Instance);

        await handler.Handle(Cmd(1000));

        lockMock.Verify(l => l.ExecuteWithLockAsync(
            It.Is<string>(k => k.Contains("lock:balance:consolidated")),
            It.IsAny<Func<Task>>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
