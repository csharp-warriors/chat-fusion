using System.Net;
using System.Net.Http.Json;
using ChatFusion.Identity.Api.Contracts;
using ChatFusion.Identity.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ChatFusion.Identity.IntegrationTests;

/// <summary>
/// Браузерный режим: refresh-токен в cookie HttpOnly, защита от CSRF по схеме double-submit.
/// </summary>
public sealed class CookieTransportTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private const string TransportHeader = "X-Refresh-Token-Transport";
    private const string CsrfHeader = "X-CSRF-Token";

    [Fact]
    public async Task Login_WithCookieTransport_SetsHttpOnlyCookieAndOmitsTokenFromBody()
    {
        var browser = CreateBrowser();
        var email = ApiClientExtensions.NewEmail();
        await browser.RegisterAsync(email);

        var response = await LoginInBrowserAsync(browser, email);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();
        tokens!.RefreshToken.Should().BeNull();
        tokens.AccessToken.Should().NotBeNullOrEmpty();

        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
        setCookies.Should().Contain(c => c.StartsWith("cf_refresh=", StringComparison.Ordinal)
            && c.Contains("httponly", StringComparison.OrdinalIgnoreCase)
            && c.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase)
            && c.Contains("path=/api/v1/auth", StringComparison.OrdinalIgnoreCase));
        setCookies.Should().Contain(c => c.StartsWith("cf_csrf=", StringComparison.Ordinal)
            && !c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Refresh_WithCookieAndCsrfHeader_RotatesCookie()
    {
        var browser = CreateBrowser();
        var email = ApiClientExtensions.NewEmail();
        await browser.RegisterAsync(email);
        var login = await LoginInBrowserAsync(browser, email);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add(CsrfHeader, ReadCsrfCookie(login));
        var response = await browser.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<TokenResponse>())!.RefreshToken.Should().BeNull();
        response.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith("cf_refresh=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Refresh_WithCookieWithoutCsrfHeader_Returns403()
    {
        var browser = CreateBrowser();
        var email = ApiClientExtensions.NewEmail();
        await browser.RegisterAsync(email);
        await LoginInBrowserAsync(browser, email);

        var response = await browser.PostAsync("/api/v1/auth/refresh", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.ReadProblemCodeAsync()).Should().Be("CsrfTokenInvalid");
    }

    [Fact]
    public async Task Refresh_WithWrongCsrfHeader_Returns403()
    {
        var browser = CreateBrowser();
        var email = ApiClientExtensions.NewEmail();
        await browser.RegisterAsync(email);
        await LoginInBrowserAsync(browser, email);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add(CsrfHeader, "forged");

        (await browser.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Logout_ClearsCookies()
    {
        var browser = CreateBrowser();
        var email = ApiClientExtensions.NewEmail();
        await browser.RegisterAsync(email);
        var login = await LoginInBrowserAsync(browser, email);
        var accessToken = (await login.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Authorization = new("Bearer", accessToken);
        var response = await browser.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith("cf_refresh=;", StringComparison.Ordinal));
    }

    private HttpClient CreateBrowser() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    private static async Task<HttpResponseMessage> LoginInBrowserAsync(HttpClient browser, string email)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password = ApiClientExtensions.DefaultPassword }),
        };
        request.Headers.Add(TransportHeader, "cookie");

        var response = await browser.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return response;
    }

    private static string ReadCsrfCookie(HttpResponseMessage response)
    {
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("cf_csrf=", StringComparison.Ordinal));
        return cookie["cf_csrf=".Length..cookie.IndexOf(';', StringComparison.Ordinal)];
    }
}
