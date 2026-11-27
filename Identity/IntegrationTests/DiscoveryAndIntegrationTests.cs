using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChatFusion.Identity.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ChatFusion.Identity.IntegrationTests;

public sealed class DiscoveryAndIntegrationTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Jwks_ContainsKeyThatSignsTokens()
    {
        var (_, tokens) = await _client.RegisterAndLoginAsync();
        var kid = new JsonWebToken(tokens.AccessToken).Kid;

        var response = await _client.GetAsync("/.well-known/jwks.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromSeconds(300));
        var jwks = new JsonWebKeySet(await response.Content.ReadAsStringAsync());
        var key = jwks.Keys.Should().ContainSingle(k => k.Kid == kid).Subject;
        key.Kty.Should().Be("RSA");
        key.Alg.Should().Be("RS256");
        key.HasPrivateKey.Should().BeFalse();
    }

    [Fact]
    public async Task OpenIdConfiguration_PointsToJwksOnRequestHost()
    {
        using var document = JsonDocument.Parse(await _client.GetStringAsync("/.well-known/openid-configuration"));

        document.RootElement.GetProperty("issuer").GetString().Should().Be(IdentityApiFactory.Issuer);
        document.RootElement.GetProperty("jwks_uri").GetString().Should().Be("http://localhost/.well-known/jwks.json");
    }

    /// <summary>
    /// Другой сервис проверяет токен только по метаданным и JWKS, без общего секрета и без вызовов на каждый запрос.
    /// </summary>
    [Fact]
    public async Task ExternalService_ValidatesTokenUsingMetadataOnly()
    {
        var (_, tokens) = await _client.RegisterAndLoginAsync();
        await using var externalService = await StartExternalServiceAsync();
        var externalClient = externalService.GetTestClient();

        var anonymous = await externalClient.GetAsync("/protected");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.Authorization = new("Bearer", tokens.AccessToken);
        var authorized = await externalClient.SendAsync(request);

        authorized.StatusCode.Should().Be(HttpStatusCode.OK);
        (await authorized.Content.ReadAsStringAsync()).Should().Be(new JsonWebToken(tokens.AccessToken).Subject + ":user");
    }

    [Fact]
    public async Task EndToEnd_RegisterLoginRefreshCallLogout()
    {
        var email = ApiClientExtensions.NewEmail();
        (await _client.RegisterAsync(email)).StatusCode.Should().Be(HttpStatusCode.Created);
        var login = await _client.LoginSuccessfullyAsync(email);

        var refreshResponse = await _client.RefreshAsync(login.RefreshToken);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = (await refreshResponse.Content.ReadFromJsonAsync<Api.Contracts.TokenResponse>())!;

        var authorized = factory.CreateClient(refreshed.AccessToken);
        (await authorized.GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await authorized.PostAsync("/api/v1/auth/logout", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _client.RefreshAsync(refreshed.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_LiveAndReady_Return200()
    {
        (await _client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenApi_DescribesBearerOnlyForProtectedOperations()
    {
        using var document = JsonDocument.Parse(await _client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");

        document.RootElement.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _)
            .Should().BeTrue();
        paths.GetProperty("/api/v1/auth/login").GetProperty("post").TryGetProperty("security", out _)
            .Should().BeFalse();
        paths.GetProperty("/api/v1/users/me").GetProperty("get").TryGetProperty("security", out _)
            .Should().BeTrue();

        (await _client.GetAsync("/scalar/v1")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ApiResponses_HaveSecurityHeaders()
    {
        var response = await _client.LoginAsync(ApiClientExtensions.NewEmail());

        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    private async Task<WebApplication> StartExternalServiceAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MetadataAddress = "http://localhost/.well-known/openid-configuration";
                options.RequireHttpsMetadata = false;
                options.BackchannelHttpHandler = factory.Server.CreateHandler();
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = IdentityApiFactory.Issuer,
                    ValidAudience = IdentityApiFactory.Audience,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                };
            });

        var app = builder.Build();
        app.UseAuthentication();
        app.Run(async context =>
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var role = context.User.IsInRole("user") ? "user" : "none";
            await context.Response.WriteAsync($"{context.User.Identity.Name}:{role}");
        });

        await app.StartAsync();
        return app;
    }
}
