namespace ChatFusion.Identity.Api.Application;

/// <summary>
/// Коды ошибок сервисов приложения. Контроллеры переводят их в HTTP-статусы.
/// </summary>
public enum ErrorCode
{
    Validation,
    InvalidCredentials,
    EmailAlreadyRegistered,
    InvalidRefreshToken,
    RefreshTokenAlreadyRotated,
    CsrfTokenInvalid,
    InvalidCurrentPassword,
    UnknownRole,
    SelfModificationForbidden,
    NotFound,
}

public sealed record Error(ErrorCode Code, string Message)
{
    public static readonly Error InvalidCredentials =
        new(ErrorCode.InvalidCredentials, "Неверный email или пароль.");

    public static readonly Error EmailAlreadyRegistered =
        new(ErrorCode.EmailAlreadyRegistered, "Пользователь с таким email уже зарегистрирован.");

    public static readonly Error InvalidRefreshToken =
        new(ErrorCode.InvalidRefreshToken, "Refresh-токен недействителен. Выполните вход заново.");

    public static readonly Error RefreshTokenAlreadyRotated =
        new(ErrorCode.RefreshTokenAlreadyRotated, "Токен уже обновлён параллельным запросом. Повторите запрос с актуальным токеном.");

    public static readonly Error CsrfTokenInvalid =
        new(ErrorCode.CsrfTokenInvalid, "Отсутствует или неверен CSRF-токен.");

    public static readonly Error InvalidCurrentPassword =
        new(ErrorCode.InvalidCurrentPassword, "Текущий пароль указан неверно.");

    public static readonly Error UserNotFound =
        new(ErrorCode.NotFound, "Пользователь не найден.");

    public static readonly Error SessionNotFound =
        new(ErrorCode.NotFound, "Сессия не найдена.");
}

/// <summary>
/// Результат операции без значения.
/// </summary>
public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error);

    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>
/// Результат операции со значением.
/// </summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, Error? error) : base(error) => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Значение недоступно: операция завершилась ошибкой.");

    public static Result<T> Success(T value) => new(value, null);

    public static new Result<T> Failure(Error error) => new(default, error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
