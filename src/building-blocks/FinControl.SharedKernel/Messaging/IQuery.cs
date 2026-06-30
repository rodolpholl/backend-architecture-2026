namespace FinControl.SharedKernel.Messaging;

/// <summary>
/// Marcador de query com retorno tipado.
/// O Wolverine descobre o handler por convenção: método Handle/HandleAsync
/// na classe {NomeDaQuery}Handler.
/// Ex.: ObterSaldoDiarioQuery : IQuery&lt;SaldoDiarioResponse&gt;
/// </summary>
public interface IQuery<TResponse>;
