namespace FinControl.SharedKernel.Messaging;

/// <summary>
/// Marcador de consumer de evento de domínio.
/// O Wolverine descobre o consumer por convenção: método Handle/HandleAsync
/// na classe {NomeDoEvento}Handler ou {NomeDoEvento}Consumer.
/// O transporte (RabbitMQ) e o roteamento são configurados no WolverineOptions.
/// </summary>
public interface IEventConsumer<in TEvent> where TEvent : Domain.DomainEvent;
