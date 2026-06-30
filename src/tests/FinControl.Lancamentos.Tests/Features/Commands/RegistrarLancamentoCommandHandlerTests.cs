using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using FinControl.Transactions.Core.Context;
using FinControl.Transactions.Core.Domain;
using FinControl.Transactions.Core.Domain.Enums;
using FinControl.Transactions.Core.Features.Commands.RegisterTransaction;
using FinControl.Transactions.Tests.Fakers;
using SharedKernelEvents = FinControl.SharedKernel.Domain.Events;

namespace FinControl.Transactions.Tests.Features.Commands;

public class RegisterTransactionCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static TransactionsDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<TransactionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static RegisterTransactionCommandHandler CreateHandler(TransactionsDbContext db) =>
        new(db, NullLogger<RegisterTransactionCommandHandler>.Instance);

    // ── Persistence ────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_Save_Transaction_In_Database()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);

        await handler.Handle(TransactionCommandFaker.ValidVenda(1500));

        db.Transactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task Should_Return_Valid_NavigationId()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);

        var response = await handler.Handle(TransactionCommandFaker.ValidVenda());

        response.NavigationId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Should_Return_Recent_CreatedAt()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);
        var before = DateTimeOffset.UtcNow;

        var response = await handler.Handle(TransactionCommandFaker.ValidVenda());

        response.CreatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task Should_Persist_Command_Data_In_Transaction()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);
        var command = TransactionCommandFaker.Build(TransactionCategory.Sale, 2500);

        await handler.Handle(command);

        var saved = db.Transactions.Single();
        saved.Amount.Should().Be(command.Amount);
        saved.Category.Should().Be(command.Category);
        saved.Description.Should().Be(command.Description);
        saved.CreatedBy.Should().Be(command.UserId);
        saved.CreatedByName.Should().Be(command.UserName);
        saved.CreatedByEmail.Should().Be(command.UserEmail);
    }

    [Fact]
    public async Task Should_Use_Current_TransactionDate_When_Default()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);
        var command = TransactionCommandFaker.ValidVenda() with { TransactionDate = default };
        var before = DateTimeOffset.UtcNow;

        await handler.Handle(command);

        var saved = db.Transactions.Single();
        saved.TransactionDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task Should_Preserve_Provided_TransactionDate()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);
        var expectedDate = DateTimeOffset.UtcNow.AddDays(-5);
        var command = TransactionCommandFaker.ValidVenda() with { TransactionDate = expectedDate };

        await handler.Handle(command);

        var saved = db.Transactions.Single();
        saved.TransactionDate.Should().BeCloseTo(expectedDate, TimeSpan.FromSeconds(1));
    }

    // ── Outbox ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_Queue_OutboxMessage_In_Same_Operation()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);

        await handler.Handle(TransactionCommandFaker.ValidVenda(1000));

        db.OutboxMessages.Should().HaveCount(1);
    }

    [Fact]
    public async Task Should_Queue_OutboxMessage_With_Correct_Exchange_And_RoutingKey()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);

        await handler.Handle(TransactionCommandFaker.ValidVenda(1000));

        var msg = db.OutboxMessages.Single();
        msg.Exchange.Should().Be("transactions.events");
        msg.RoutingKey.Should().Be("transaction.created");
        msg.MessageType.Should().Be(nameof(SharedKernelEvents.TransactionRegisteredMessage));
    }

    [Fact]
    public async Task Should_Queue_Payload_With_Correct_Amount()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);
        var command = TransactionCommandFaker.ValidVenda(3333);

        await handler.Handle(command);

        var payload = db.OutboxMessages.Single().Payload;
        var eventData = JsonSerializer.Deserialize<SharedKernelEvents.TransactionRegisteredMessage>(payload, JsonOptions)!;
        eventData.Amount.Should().Be(3333);
    }

    [Fact]
    public async Task Should_Queue_Payload_With_Command_CorrelationId()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);
        var command = TransactionCommandFaker.ValidVenda();

        await handler.Handle(command);

        var payload = db.OutboxMessages.Single().Payload;
        var eventData = JsonSerializer.Deserialize<SharedKernelEvents.TransactionRegisteredMessage>(payload, JsonOptions)!;
        eventData.CorrelationId.Should().Be(command.CorrelationId);
    }

    [Fact]
    public async Task OutboxMessage_Should_Start_Not_Delivered()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);

        await handler.Handle(TransactionCommandFaker.ValidVenda());

        db.OutboxMessages.Single().DeliveredAt.Should().BeNull();
    }

    // ── Multiple Transactions ────────────────────────────────────────────────

    [Fact]
    public async Task Should_Persist_Multiple_Independent_Transactions()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);

        await handler.Handle(TransactionCommandFaker.ValidVenda(1000));
        await handler.Handle(TransactionCommandFaker.ValidDebito(TransactionCategory.Return, -500));
        await handler.Handle(TransactionCommandFaker.ValidVenda(2500));

        db.Transactions.Should().HaveCount(3);
        db.OutboxMessages.Should().HaveCount(3);
    }
}
