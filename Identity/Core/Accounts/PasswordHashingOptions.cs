using System.ComponentModel.DataAnnotations;

namespace ChatFusion.Identity.Core.Accounts;

/// <summary>
/// Параметры хеширования паролей. Секция <c>PasswordHashing</c>.
/// Используется сервисом и мигратором, поэтому администратор создаётся с теми же параметрами, что и пользователи.
/// </summary>
public sealed class PasswordHashingOptions
{
    public const string SectionName = "PasswordHashing";

    [Range(10_000, 10_000_000)]
    public int Iterations { get; set; } = 600_000;
}
