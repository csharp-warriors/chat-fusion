using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;

namespace ChatFusion.Identity.Core.Accounts;

/// <summary>
/// Проверка и нормализация email.
/// </summary>
public interface IEmailNormalizer
{
    /// <summary>
    /// Убирает пробелы по краям и проверяет формат.
    /// </summary>
    /// <param name="input">Введённый email.</param>
    /// <param name="email">Email без пробелов по краям, для хранения и отображения.</param>
    /// <param name="normalizedEmail">Email в верхнем регистре, для поиска без учёта регистра.</param>
    bool TryNormalize(
        string? input,
        [NotNullWhen(true)] out string? email,
        [NotNullWhen(true)] out string? normalizedEmail);
}

public sealed class EmailNormalizer : IEmailNormalizer
{
    public const int MaxLength = 256;

    public bool TryNormalize(
        string? input,
        [NotNullWhen(true)] out string? email,
        [NotNullWhen(true)] out string? normalizedEmail)
    {
        email = null;
        normalizedEmail = null;

        var trimmed = input?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength || trimmed.Any(char.IsWhiteSpace))
        {
            return false;
        }

        // MailAddress принимает и формат «Имя <адрес>», поэтому адрес должен совпадать с вводом целиком
        if (!MailAddress.TryCreate(trimmed, out var address) || address.Address != trimmed)
        {
            return false;
        }

        email = trimmed;
        normalizedEmail = trimmed.ToUpperInvariant();
        return true;
    }
}
