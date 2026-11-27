namespace ChatFusion.Identity.Migrator;

/// <summary>
/// Первый администратор, создаваемый командой <c>migrate</c>. Секция <c>Admin</c>.
/// </summary>
public sealed class AdminSeedOptions
{
    public const string SectionName = "Admin";

    public string? Email { get; set; }

    /// <summary>Файл с паролем (Docker secret). Имеет приоритет над <see cref="Password"/>.</summary>
    public string? PasswordFile { get; set; }

    /// <summary>Пароль напрямую. Предназначен для разработки и тестов.</summary>
    public string? Password { get; set; }
}
