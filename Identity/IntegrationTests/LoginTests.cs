using System.Net;
using ChatFusion.Identity.Core.Persistence;
using ChatFusion.Identity.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ChatFusion.Identity.IntegrationTests;

public sealed class LoginTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokensAndCreatesSession()
    {
        var email = ApiClientExtensions.NewEmail();
        await _client.RegisterAsync(email);

        var tokens = await _client.LoginSuccessfullyAsync(email.ToUpperInvariant());

        tokens.TokenType.Should().Be("Bearer");
        tokens.RefreshToken.Should().NotBeNullOrEmpty();
        tokens.AccessTokenExpiresAt.Should().BeCloseTo(factory.Time.GetUtcNow().AddMinutes(15), TimeSpan.FromSeconds(1));

        var jwt = new JsonWebToken(tokens.AccessToken);
        jwt.Issuer.Should().Be(IdentityApiFactory.Issuer);
        var sessionId = Guid.Parse(jwt.GetClaim("sid").Value);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityServiceDbContext>();
        (await db.UserSessions.AnyAsync(s => s.Id == sessionId && s.RevokedAt == null)).Should().BeTrue();
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownUser_ReturnSameResponse()
    {
        var email = ApiClientExtensions.NewEmail();
        await _client.RegisterAsync(email);

        var wrongPassword = await _client.LoginAsync(email, "Wrong-Passw0rd");
        var unknownUser = await _client.LoginAsync(ApiClientExtensions.NewEmail());

        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownUser.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongPassword.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await wrongPassword.ReadProblemCodeAsync()).Should().Be("InvalidCredentials");
        (await unknownUser.ReadProblemCodeAsync()).Should().Be("InvalidCredentials");
    }

    [Fact]
    public async Task Login_AfterFiveFailedAttempts_IsLockedUntilLockoutEnds()
    {
        var email = ApiClientExtensions.NewEmail();
        await _client.RegisterAsync(email);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await _client.LoginAsync(email, "Wrong-Passw0rd")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var whileLocked = await _client.LoginAsync(email);
        whileLocked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await whileLocked.ReadProblemCodeAsync()).Should().Be("InvalidCredentials");

        factory.Time.Advance(TimeSpan.FromMinutes(16));

        (await _client.LoginAsync(email)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_SuccessfulLogin_ResetsFailedAttempts()
    {
        var email = ApiClientExtensions.NewEmail();
        await _client.RegisterAsync(email);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            await _client.LoginAsync(email, "Wrong-Passw0rd");
        }

        await _client.LoginSuccessfullyAsync(email);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            await _client.LoginAsync(email, "Wrong-Passw0rd");
        }

        (await _client.LoginAsync(email)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_StoresClientInfoInSession()
    {
        var email = ApiClientExtensions.NewEmail();
        await _client.RegisterAsync(email);
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("ChatFusionTests/1.0");

        var tokens = await _client.LoginSuccessfullyAsync(email);
        var sessionId = Guid.Parse(new JsonWebToken(tokens.AccessToken).GetClaim("sid").Value);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityServiceDbContext>();
        var session = await db.UserSessions.SingleAsync(s => s.Id == sessionId);
        session.UserAgent.Should().Contain("ChatFusionTests/1.0");
    }
}
