using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace ChatFusion.Identity.Core.Accounts;

/// <summary>
/// Результат проверки пароля.
/// </summary>
public enum PasswordCheckResult
{
    Failed,
    Success,

    /// <summary>Пароль верный, но хеш создан с устаревшими параметрами и его нужно пересчитать.</summary>
    SuccessRehashNeeded,
}

/// <summary>
/// Хеширование паролей.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordCheckResult Verify(string passwordHash, string password);

    /// <summary>
    /// Выполняет проверку с той же стоимостью, что и <see cref="Verify"/>, по фиктивному хешу.
    /// Нужна, чтобы время ответа не выдавало отсутствие пользователя.
    /// </summary>
    void SimulateVerification(string password);
}

/// <summary>
/// PBKDF2-HMAC-SHA256 на встроенной криптографии .NET.
/// Формат хеша: <c>v1$итерации$соль$хеш</c>, соль и хеш в Base64.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string FormatVersion = "v1";
    private const char Separator = '$';
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MaxIterations = 10_000_000;

    private readonly int _iterations;
    private readonly Lazy<string> _dummyHash;

    public Pbkdf2PasswordHasher(IOptions<PasswordHashingOptions> options)
    {
        _iterations = options.Value.Iterations;
        _dummyHash = new Lazy<string>(() => Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(SaltSize))));
    }

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, HashAlgorithmName.SHA256, HashSize);

        return string.Join(
            Separator,
            FormatVersion,
            _iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public PasswordCheckResult Verify(string passwordHash, string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        if (!TryParse(passwordHash, out var iterations, out var salt, out var expected))
        {
            return PasswordCheckResult.Failed;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            return PasswordCheckResult.Failed;
        }

        return iterations < _iterations ? PasswordCheckResult.SuccessRehashNeeded : PasswordCheckResult.Success;
    }

    public void SimulateVerification(string password) => Verify(_dummyHash.Value, password);

    private static bool TryParse(string? passwordHash, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0;
        salt = [];
        hash = [];

        var parts = passwordHash?.Split(Separator);
        if (parts is not [FormatVersion, var iterationsPart, var saltPart, var hashPart]
            || !int.TryParse(iterationsPart, NumberStyles.None, CultureInfo.InvariantCulture, out iterations)
            || iterations is <= 0 or > MaxIterations)
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(saltPart);
            hash = Convert.FromBase64String(hashPart);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && hash.Length > 0;
    }
}
