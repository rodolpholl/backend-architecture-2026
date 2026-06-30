namespace FinControl.SharedKernel.Domain;

public sealed class DomainError
{
    public static readonly DomainError None = new(string.Empty, string.Empty, ErrorType.Failure);
    public static readonly DomainError NullValue = new("General.Null", "Um valor nulo foi fornecido.", ErrorType.Failure);

    public string Code { get; }
    public string Description { get; }
    public ErrorType Type { get; }

    private DomainError(string code, string description, ErrorType type)
    {
        Code = code;
        Description = description;
        Type = type;
    }

    public static DomainError Failure(string code, string description) =>
        new(code, description, ErrorType.Failure);

    public static DomainError NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);

    public static DomainError Validation(string code, string description) =>
        new(code, description, ErrorType.Validation);

    public static DomainError Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);

    public static DomainError Unauthorized(string code, string description) =>
        new(code, description, ErrorType.Unauthorized);

    public static implicit operator Result(DomainError error) => Result.Failure(error);
}

public enum ErrorType
{
    Failure,
    NotFound,
    Validation,
    Conflict,
    Unauthorized
}
