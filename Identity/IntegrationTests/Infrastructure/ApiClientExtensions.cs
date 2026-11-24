using System.Net;
using System.Net.Http.Json;
using ChatFusion.Identity.Api.Contracts;

namespace ChatFusion.Identity.IntegrationTests.Infrastructure;

/// <summary>
/// Короткие вызовы API для тестов.
/// </summary>
public static class ApiClientExtensions
{
    public const string DefaultPassword = "Str0ng-Passw0rd";

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@chatfusion.test";

    public static Task<HttpResponseMessage> RegisterAsync(this HttpClient client, string email, string password = DefaultPassword) =>
        client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password = DefaultPassword) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string? refreshToken) =>
        client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });

    /// <summary>
    /// Регистрирует пользователя и выполняет вход.
    /// </summary>
    public static async Task<(string Email, TokenResponse Tokens)> RegisterAndLoginAsync(this HttpClient client)
    {
        var email = NewEmail();
        (await client.RegisterAsync(email)).StatusCode.Should().Be(HttpStatusCode.Created);

        return (email, await client.LoginSuccessfullyAsync(email));
    }

    public static async Task<TokenResponse> LoginSuccessfullyAsync(this HttpClient client, string email, string password = DefaultPassword)
    {
        var response = await client.LoginAsync(email, password);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    public static async Task<string?> ReadProblemCodeAsync(this HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>();
        return problem?.GetValueOrDefault("code")?.ToString();
    }
}
