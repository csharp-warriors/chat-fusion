using System.Security.Cryptography;
using System.Text;
using ChatFusion.Identity.Api.Application;
using ChatFusion.Identity.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ChatFusion.Identity.Api.Infrastructure.Web;

/// <summary>
/// Передача refresh-токена браузерным клиентам через cookie <c>HttpOnly</c> с защитой от CSRF
/// по схеме double-submit: значение cookie <c>cf_csrf</c> клиент повторяет в заголовке <c>X-CSRF-Token</c>.
/// </summary>
public sealed class RefreshTokenCookies(IOptions<RefreshCookieOptions> options)
{
    public const string RefreshTokenCookieName = "cf_refresh";
    public const string CsrfCookieName = "cf_csrf";
    public const string CsrfHeaderName = "X-CSRF-Token";
    public const string TransportHeaderName = "X-Refresh-Token-Transport";
    public const string CookieTransport = "cookie";

    private const string RefreshTokenCookiePath = "/api/v1/auth";
    private const string CsrfCookiePath = "/";
    private const int CsrfTokenSize = 32;

    public static bool IsCookieTransportRequested(HttpRequest request) =>
        string.Equals(request.Headers[TransportHeaderName], CookieTransport, StringComparison.OrdinalIgnoreCase);

    public static string? ReadRefreshToken(HttpRequest request) =>
        request.Cookies.TryGetValue(RefreshTokenCookieName, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    public static bool IsCsrfTokenValid(HttpRequest request)
    {
        var cookie = request.Cookies[CsrfCookieName];
        var header = request.Headers[CsrfHeaderName].ToString();

        return !string.IsNullOrEmpty(cookie)
            && !string.IsNullOrEmpty(header)
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(cookie), Encoding.UTF8.GetBytes(header));
    }

    public void Write(HttpResponse response, TokenPair tokens)
    {
        response.Cookies.Append(
            RefreshTokenCookieName,
            tokens.RefreshToken,
            CreateCookieOptions(RefreshTokenCookiePath, httpOnly: true, tokens.RefreshTokenExpiresAt));

        response.Cookies.Append(
            CsrfCookieName,
            Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(CsrfTokenSize)),
            CreateCookieOptions(CsrfCookiePath, httpOnly: false, tokens.RefreshTokenExpiresAt));
    }

    public void Clear(HttpResponse response)
    {
        response.Cookies.Delete(RefreshTokenCookieName, CreateCookieOptions(RefreshTokenCookiePath, httpOnly: true, expires: null));
        response.Cookies.Delete(CsrfCookieName, CreateCookieOptions(CsrfCookiePath, httpOnly: false, expires: null));
    }

    private CookieOptions CreateCookieOptions(string path, bool httpOnly, DateTimeOffset? expires) => new()
    {
        HttpOnly = httpOnly,
        Secure = options.Value.Secure,
        SameSite = SameSiteMode.Strict,
        Path = path,
        Expires = expires,
        IsEssential = true,
    };
}
