using System.ComponentModel.DataAnnotations;
using ChatFusion.Identity.Core.Accounts;
using ChatFusion.Identity.Api.Infrastructure.Security;

namespace ChatFusion.Identity.Api.Contracts;

/// <summary>Запрос на регистрацию.</summary>
public sealed class RegisterRequest
{
    /// <summary>Email пользователя.</summary>
    [Required(ErrorMessage = "Email обязателен.")]
    [EmailAddress(ErrorMessage = "Некорректный email.")]
    [StringLength(EmailNormalizer.MaxLength, ErrorMessage = "Email не должен быть длиннее {1} символов.")]
    public string Email { get; init; } = "";

    /// <summary>Пароль: от 8 до 128 символов.</summary>
    [Required(ErrorMessage = "Пароль обязателен.")]
    [StringLength(PasswordPolicy.MaxLength, MinimumLength = PasswordPolicy.MinLength, ErrorMessage = "Длина пароля должна быть от {2} до {1} символов.")]
    public string Password { get; init; } = "";
}

/// <summary>Запрос на вход.</summary>
public sealed class LoginRequest
{
    /// <summary>Email пользователя.</summary>
    [Required(ErrorMessage = "Email обязателен.")]
    [StringLength(EmailNormalizer.MaxLength, ErrorMessage = "Email не должен быть длиннее {1} символов.")]
    public string Email { get; init; } = "";

    /// <summary>Пароль.</summary>
    [Required(ErrorMessage = "Пароль обязателен.")]
    [StringLength(PasswordPolicy.MaxLength, ErrorMessage = "Пароль не должен быть длиннее {1} символов.")]
    public string Password { get; init; } = "";
}

/// <summary>Запрос на обновление токенов.</summary>
public sealed class RefreshRequest
{
    /// <summary>
    /// Refresh-токен. Браузерный клиент не передаёт его в теле: токен берётся из cookie,
    /// а в заголовке <c>X-CSRF-Token</c> передаётся значение cookie <c>cf_csrf</c>.
    /// </summary>
    [StringLength(RefreshTokenGenerator.MaxTokenLength)]
    public string? RefreshToken { get; init; }
}

/// <summary>Выданные токены.</summary>
public sealed class TokenResponse
{
    /// <summary>Access-токен (JWT) для заголовка <c>Authorization: Bearer</c>.</summary>
    public required string AccessToken { get; init; }

    /// <summary>Тип токена, всегда <c>Bearer</c>.</summary>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>Окончание срока действия access-токена.</summary>
    public required DateTimeOffset AccessTokenExpiresAt { get; init; }

    /// <summary>Refresh-токен. Не возвращается, если он передан в cookie.</summary>
    public string? RefreshToken { get; init; }

    /// <summary>Окончание срока действия refresh-токена.</summary>
    public required DateTimeOffset RefreshTokenExpiresAt { get; init; }
}
