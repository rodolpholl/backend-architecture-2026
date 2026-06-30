namespace FinControl.SharedKernel.Messaging;

/// <summary>
/// Marcador de comando sem retorno tipado.
/// O Wolverine descobre o handler por convenção: método Handle/HandleAsync
/// na classe {NomeDoCommand}Handler, sem necessidade de herdar qualquer interface.
/// </summary>
public interface ICommand;

/// <summary>
/// Marcador de comando com retorno tipado.
/// Ex.: RegisterTransactionCommand : ICommand&lt;LancamentoResponse&gt;
/// </summary>
public interface ICommand<TResponse>;
