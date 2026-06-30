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

    // ── Persistência ────────────────────────────────────────────────────────

    [Fact]
    public async Task Deve_Salvar_Lancamento_No_Banco()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);

        await handler.Handle(TransactionCommandFaker.ValidVenda(1500));

        db.Transactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task Deve_Retornar_NavigationId_Valido()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);

        var response = await handler.Handle(TransactionCommandFaker.ValidVenda());

        response.NavigationId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Deve_Retornar_CriadoEm_Recente()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);
        var antes = DateTimeOffset.UtcNow;

        var response = await handler.Handle(TransactionCommandFaker.ValidVenda());

        response.CreatedAt.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public async Task Deve_Persistir_Dados_Do_Comando_No_Lancamento()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);
        var command = TransactionCommandFaker.Build(TransactionCategory.Sale, 2500);

        await handler.Handle(command);

        var salvo = db.Transactions.Single();
        salvo.Amount.Should().Be(command.Amount);
        salvo.Category.Should().Be(command.Category);
        salvo.Description.Should().Be(command.Description);
        salvo.CreatedBy.Should().Be(command.UserId);
        salvo.CreatedByName.Should().Be(command.UserName);
        salvo.CreatedByEmail.Should().Be(command.UserEmail);
    }

    [Fact]
    public async Task Deve_Usar_TransactionDate_Atual_Quando_Default()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);
        var command = TransactionCommandFaker.ValidVenda() with { TransactionDate = default };
        var antes = DateTimeOffset.UtcNow;

        await handler.Handle(command);

        var salvo = db.Transactions.Single();
        salvo.TransactionDate.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public async Task Deve_Preservar_TransactionDate_Informada()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);
        var dataEsperada = DateTimeOffset.UtcNow.AddDays(-5);
        var command = TransactionCommandFaker.ValidVenda() with { TransactionDate = dataEsperada };

        await handler.Handle(command);

        var salvo = db.Transactions.Single();
        salvo.TransactionDate.Should().BeCloseTo(dataEsperada, TimeSpan.FromSeconds(1));
    }

    // ── Outbox ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deve_Enfileirar_OutboxMessage_Na_Mesma_Operacao()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);

        await handler.Handle(TransactionCommandFaker.ValidVenda(1000));

        db.OutboxMessages.Should().HaveCount(1);
    }

    [Fact]
    public async Task Deve_Enfileirar_OutboxMessage_Com_Exchange_E_RoutingKey_Corretos()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);

        await handler.Handle(TransactionCommandFaker.ValidVenda(1000));

        var msg = db.OutboxMessages.Single();
        msg.Exchange.Should().Be("lancamentos.events");
        msg.RoutingKey.Should().Be("lancamento.criado");
        msg.MessageType.Should().Be(nameof(SharedKernelEvents.TransactionRegisteredMessage));
    }

    [Fact]
    public async Task Deve_Enfileirar_Payload_Com_Valor_Correto()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);
        var command = TransactionCommandFaker.ValidVenda(3333);

        await handler.Handle(command);

        var payload = db.OutboxMessages.Single().Payload;
        var evento = JsonSerializer.Deserialize<SharedKernelEvents.TransactionRegisteredMessage>(payload, JsonOptions)!;
        evento.Amount.Should().Be(3333);
    }

    [Fact]
    public async Task Deve_Enfileirar_Payload_Com_CorrelationId_Do_Comando()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);
        var command = TransactionCommandFaker.ValidVenda();

        await handler.Handle(command);

        var payload = db.OutboxMessages.Single().Payload;
        var evento = JsonSerializer.Deserialize<SharedKernelEvents.TransactionRegisteredMessage>(payload, JsonOptions)!;
        evento.CorrelationId.Should().Be(command.CorrelationId);
    }

    [Fact]
    public async Task OutboxMessage_Deve_Iniciar_Nao_Entregue()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);

        await handler.Handle(TransactionCommandFaker.ValidVenda());

        db.OutboxMessages.Single().DeliveredAt.Should().BeNull();
    }

    // ── Múltiplos lançamentos ────────────────────────────────────────────────

    [Fact]
    public async Task Deve_Persistir_Multiplos_Lancamentos_Independentes()
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
