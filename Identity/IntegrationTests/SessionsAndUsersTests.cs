using System.Net;
using System.Net.Http.Json;
using ChatFusion.Identity.Api.Contracts;
using ChatFusion.Identity.IntegrationTests.Infrastructure;

namespace ChatFusion.Identity.IntegrationTests;

public sealed class SessionsAndUsersTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private readonly HttpClient _anonymous = factory.CreateClient();

    [Fact]
    public async Task ProtectedEndpoints_WithoutToken_Return401()
    {
        (await _anonymous.GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _anonymous.GetAsync("/api/v1/users/me/sessions")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _anonymous.PostAsync("/api/v1/auth/logout", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoints_WithForgedToken_Return401()
    {
        var client = factory.CreateClient("eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJl");

        (await client.GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_ReturnsProfile()
    {
        var (email, tokens) = await _anonymous.RegisterAndLoginAsync();

        var profile = await factory.CreateClient(tokens.AccessToken).GetFromJsonAsync<UserResponse>("/api/v1/users/me");

        profile!.Email.Should().Be(email);
        profile.Roles.Should().Equal("user");
    }

    [Fact]
    public async Task AccessToken_AfterExpiry_Returns401()
    {
        var (_, tokens) = await _anonymous.RegisterAndLoginAsync();
        var client = factory.CreateClient(tokens.AccessToken);

        factory.Time.Advance(TimeSpan.FromMinutes(16));

        (await client.GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesSession()
    {
        var (_, tokens) = await _anonymous.RegisterAndLoginAsync();

        var logout = await factory.CreateClient(tokens.AccessToken).PostAsync("/api/v1/auth/logout", null);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _anonymous.RefreshAsync(tokens.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sessions_ListAndRevokeOther()
    {
        var (email, first) = await _anonymous.RegisterAndLoginAsync();
        var second = await _anonymous.LoginSuccessfullyAsync(email);
        var client = factory.CreateClient(second.AccessToken);

        var sessions = await client.GetFromJsonAsync<List<SessionResponse>>("/api/v1/users/me/sessions");
        sessions.Should().HaveCount(2);
        sessions!.Should().ContainSingle(s => s.IsCurrent);
        var other = sessions.Single(s => !s.IsCurrent);

        (await client.DeleteAsync($"/api/v1/users/me/sessions/{other.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _anonymous.RefreshAsync(first.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetFromJsonAsync<List<SessionResponse>>("/api/v1/users/me/sessions")).Should().ContainSingle();
    }

    [Fact]
    public async Task Sessions_CannotRevokeSessionOfAnotherUser()
    {
        var (_, victim) = await _anonymous.RegisterAndLoginAsync();
        var (_, attacker) = await _anonymous.RegisterAndLoginAsync();
        var victimSessions = await factory.CreateClient(victim.AccessToken)
            .GetFromJsonAsync<List<SessionResponse>>("/api/v1/users/me/sessions");

        var response = await factory.CreateClient(attacker.AccessToken)
            .DeleteAsync($"/api/v1/users/me/sessions/{victimSessions!.Single().Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _anonymous.RefreshAsync(victim.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Sessions_RevokeAllExceptCurrent()
    {
        var (email, current) = await _anonymous.RegisterAndLoginAsync();
        var otherA = await _anonymous.LoginSuccessfullyAsync(email);
        var otherB = await _anonymous.LoginSuccessfullyAsync(email);

        var response = await factory.CreateClient(current.AccessToken).DeleteAsync("/api/v1/users/me/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _anonymous.RefreshAsync(otherA.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _anonymous.RefreshAsync(otherB.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _anonymous.RefreshAsync(current.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Returns400()
    {
        var (_, tokens) = await _anonymous.RegisterAndLoginAsync();

        var response = await factory.CreateClient(tokens.AccessToken).PutAsJsonAsync(
            "/api/v1/users/me/password",
            new { currentPassword = "Wrong-Passw0rd", newPassword = "N3w-Passw0rd" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadProblemCodeAsync()).Should().Be("InvalidCurrentPassword");
    }

    [Fact]
    public async Task ChangePassword_RevokesOtherSessionsAndRequiresNewPassword()
    {
        var (email, current) = await _anonymous.RegisterAndLoginAsync();
        var other = await _anonymous.LoginSuccessfullyAsync(email);

        var response = await factory.CreateClient(current.AccessToken).PutAsJsonAsync(
            "/api/v1/users/me/password",
            new { currentPassword = ApiClientExtensions.DefaultPassword, newPassword = "N3w-Passw0rd" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _anonymous.RefreshAsync(other.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _anonymous.RefreshAsync(current.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _anonymous.LoginAsync(email)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _anonymous.LoginAsync(email, "N3w-Passw0rd")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
