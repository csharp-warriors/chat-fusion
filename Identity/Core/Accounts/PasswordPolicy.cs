namespace ChatFusion.Identity.Core.Accounts;

/// <summary>
/// Требования к паролю.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static bool IsSatisfiedBy(string? password) =>
        password is { Length: >= MinLength and <= MaxLength };
}
