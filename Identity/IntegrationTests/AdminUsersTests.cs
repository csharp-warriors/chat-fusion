using System.Net;
using System.Net.Http.Json;
using ChatFusion.Identity.Api.Contracts;
using ChatFusion.Identity.IntegrationTests.Infrastructure;

namespace ChatFusion.Identity.IntegrationTests;

public sealed class AdminUsersTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private readonly HttpClient _anonymous = factory.CreateClient();

    [Fact]
    public async Task AdminEndpoints_RequireAdminRole()
    {
        var (_, user) = await _anonymous.RegisterAndLoginAsync();

        (await _anonymous.GetAsync("/api/v1/admin/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.CreateClient(user.AccessToken).GetAsync("/api/v1/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SeededAdministrator_CanListUsersWithPaging()
    {
        var admin = await AdminClientAsync();
        var (email, _) = await _anonymous.RegisterAndLoginAsync();

        var page = await admin.GetFromJsonAsync<PagedResponse<AdminUserResponse>>(
            $"/api/v1/admin/users?page=1&pageSize=1&email={Uri.EscapeDataString(email.ToUpperInvariant())}");

        page!.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle().Which.Email.Should().Be(email);

        var all = await admin.GetFromJsonAsync<PagedResponse<AdminUserResponse>>("/api/v1/admin/users?pageSize=100");
        all!.Items.Should().Contain(u => u.Email == IdentityApiFactory.AdminEmail && u.Roles.Contains("admin"));
    }

    [Fact]
    public async Task List_InvalidPaging_Returns400()
    {
        var admin = await AdminClientAsync();

        (await admin.GetAsync("/api/v1/admin/users?page=0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/v1/admin/users?pageSize=101")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetRoles_GrantsAdminRoleOnNextLogin()
    {
        var admin = await AdminClientAsync();
        var (email, _) = await _anonymous.RegisterAndLoginAsync();
        var userId = await FindUserIdAsync(admin, email);

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/users/{userId}/roles", new { roles = new[] { "user", "admin" } });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var tokens = await _anonymous.LoginSuccessfullyAsync(email);
        (await factory.CreateClient(tokens.AccessToken).GetAsync("/api/v1/admin/users")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetRoles_UnknownRole_Returns400()
    {
        var admin = await AdminClientAsync();
        var (email, _) = await _anonymous.RegisterAndLoginAsync();
        var userId = await FindUserIdAsync(admin, email);

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/users/{userId}/roles", new { roles = new[] { "superuser" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadProblemCodeAsync()).Should().Be("UnknownRole");
    }

    [Fact]
    public async Task SetRoles_RemovingOwnAdminRole_Returns409()
    {
        var admin = await AdminClientAsync();
        var adminId = await FindUserIdAsync(admin, IdentityApiFactory.AdminEmail);

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/users/{adminId}/roles", new { roles = new[] { "user" } });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SetRoles_UnknownUser_Returns404()
    {
        var admin = await AdminClientAsync();

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/users/{Guid.NewGuid()}/roles", new { roles = new[] { "user" } });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Block_RevokesSessionsAndPreventsLogin_UnblockRestoresAccess()
    {
        var admin = await AdminClientAsync();
        var (email, tokens) = await _anonymous.RegisterAndLoginAsync();
        var userId = await FindUserIdAsync(admin, email);

        (await admin.PostAsync($"/api/v1/admin/users/{userId}/block", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _anonymous.RefreshAsync(tokens.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var blockedLogin = await _anonymous.LoginAsync(email);
        blockedLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await blockedLogin.ReadProblemCodeAsync()).Should().Be("InvalidCredentials");

        (await admin.PostAsync($"/api/v1/admin/users/{userId}/unblock", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _anonymous.LoginAsync(email)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Block_Self_Returns409()
    {
        var admin = await AdminClientAsync();
        var adminId = await FindUserIdAsync(admin, IdentityApiFactory.AdminEmail);

        (await admin.PostAsync($"/api/v1/admin/users/{adminId}/block", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var tokens = await _anonymous.LoginSuccessfullyAsync(IdentityApiFactory.AdminEmail, IdentityApiFactory.AdminPassword);
        return factory.CreateClient(tokens.AccessToken);
    }

    private static async Task<Guid> FindUserIdAsync(HttpClient admin, string email)
    {
        var page = await admin.GetFromJsonAsync<PagedResponse<AdminUserResponse>>(
            $"/api/v1/admin/users?email={Uri.EscapeDataString(email)}");

        return page!.Items.Single(u => u.Email == email).Id;
    }
}
