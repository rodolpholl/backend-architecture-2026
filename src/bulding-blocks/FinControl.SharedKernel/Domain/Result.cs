namespace FinControl.SharedKernel.Domain;

public class Result
{
    public bool        IsSuccess { get; }
    public bool        IsFailure => !IsSuccess;
    public DomainError Error     { get; }

    protected Result(bool isSuccess, DomainError error)
    {
        if (isSuccess  && error != DomainError.None)
            throw new InvalidOperationException("Resultado de sucesso não pode ter erro.");
        if (!isSuccess && error == DomainError.None)
            throw new InvalidOperationException("Resultado de falha deve ter um erro.");

        IsSuccess = isSuccess;
        Error     = error;
    }

    public static Result         Success()                      => new(true,  DomainError.None);
    public static Result         Failure(DomainError error)     => new(false, error);
    public static Result<TValue> Success<TValue>(TValue value)  => new(value, true,  DomainError.None);
    public static Result<TValue> Failure<TValue>(DomainError e) => new(default, false, e);
}

public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, DomainError error) : base(isSuccess, error)
        => _value = value;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Não é possível acessar o valor de um resultado de falha.");

    public static implicit operator Result<TValue>(TValue value)      => Result.Success(value);
    public static implicit operator Result<TValue>(DomainError error) => Result.Failure<TValue>(error);
}
