using System.ComponentModel.DataAnnotations;
using ChatFusion.Identity.Core.Accounts;
using ChatFusion.Identity.Api.Infrastructure.Security;

namespace ChatFusion.Identity.Api.Contracts;

/// <summary>Учётная запись пользователя.</summary>
/// <param name="Id">Идентификатор, совпадает с claim <c>sub</c>.</param>
/// <param name="Email">Email.</param>
/// <param name="Roles">Коды ролей.</param>
/// <param name="CreatedAt">Дата регистрации.</param>
/// <param name="PasswordChangedAt">Дата последней смены пароля.</param>
public sealed record UserResponse(
    Guid Id,
    string Email,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PasswordChangedAt);

/// <summary>Запрос на смену пароля.</summary>
public sealed class ChangePasswordRequest
{
    /// <summary>Текущий пароль.</summary>
    [Required(ErrorMessage = "Текущий пароль обязателен.")]
    [StringLength(PasswordPolicy.MaxLength, ErrorMessage = "Пароль не должен быть длиннее {1} символов.")]
    public string CurrentPassword { get; init; } = "";

    /// <summary>Новый пароль: от 8 до 128 символов.</summary>
    [Required(ErrorMessage = "Новый пароль обязателен.")]
    [StringLength(PasswordPolicy.MaxLength, MinimumLength = PasswordPolicy.MinLength, ErrorMessage = "Длина пароля должна быть от {2} до {1} символов.")]
    public string NewPassword { get; init; } = "";
}

/// <summary>Активная сессия пользователя.</summary>
/// <param name="Id">Идентификатор сессии, совпадает с claim <c>sid</c>.</param>
/// <param name="CreatedAt">Время входа.</param>
/// <param name="LastSeenAt">Последнее обновление токенов.</param>
/// <param name="ExpiresAt">Абсолютный срок жизни сессии.</param>
/// <param name="IpAddress">IP-адрес при входе.</param>
/// <param name="UserAgent">Клиент.</param>
/// <param name="IsCurrent">Сессия текущего запроса.</param>
public sealed record SessionResponse(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset ExpiresAt,
    string? IpAddress,
    string? UserAgent,
    bool IsCurrent);

/// <summary>Пользователь в списке администратора.</summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="Email">Email.</param>
/// <param name="Roles">Коды ролей.</param>
/// <param name="IsBlocked">Заблокирован администратором.</param>
/// <param name="LockoutEnd">Окончание временной блокировки после неудачных попыток входа.</param>
/// <param name="CreatedAt">Дата регистрации.</param>
public sealed record AdminUserResponse(
    Guid Id,
    string Email,
    IReadOnlyList<string> Roles,
    bool IsBlocked,
    DateTimeOffset? LockoutEnd,
    DateTimeOffset CreatedAt);

/// <summary>Страница списка.</summary>
/// <param name="Items">Элементы страницы.</param>
/// <param name="Page">Номер страницы, начиная с 1.</param>
/// <param name="PageSize">Размер страницы.</param>
/// <param name="TotalCount">Общее число элементов.</param>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

/// <summary>Запрос на назначение ролей.</summary>
public sealed class SetRolesRequest
{
    /// <summary>Полный набор ролей пользователя, например <c>["user", "admin"]</c>.</summary>
    [Required(ErrorMessage = "Список ролей обязателен.")]
    [MinLength(1, ErrorMessage = "Нужно указать хотя бы одну роль.")]
    public List<string> Roles { get; init; } = [];
}
