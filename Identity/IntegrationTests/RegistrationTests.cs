using System.Net;
using System.Net.Http.Json;
using ChatFusion.Identity.Api.Contracts;
using ChatFusion.Identity.IntegrationTests.Infrastructure;

namespace ChatFusion.Identity.IntegrationTests;

public sealed class RegistrationTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_NewUser_Returns201WithUserRole()
    {
        var email = ApiClientExtensions.NewEmail();

        var response = await _client.RegisterAsync(email);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        user!.Email.Should().Be(email);
        user.Roles.Should().Equal("user");
        user.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Register_SameEmailInDifferentCase_Returns409()
    {
        var email = ApiClientExtensions.NewEmail();
        (await _client.RegisterAsync(email)).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await _client.RegisterAsync(email.ToUpperInvariant());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.ReadProblemCodeAsync()).Should().Be("EmailAlreadyRegistered");
    }

    [Fact]
    public async Task Register_ConcurrentRequestsWithSameEmail_OnlyOneSucceeds()
    {
        var email = ApiClientExtensions.NewEmail();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _client.RegisterAsync(email)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(4);
    }

    [Theory]
    [InlineData("not-an-email", ApiClientExtensions.DefaultPassword)]
    [InlineData("user@chatfusion.test", "short")]
    [InlineData("", ApiClientExtensions.DefaultPassword)]
    public async Task Register_InvalidData_Returns400(string email, string password)
    {
        var response = await _client.RegisterAsync(email, password);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_TooLongPassword_Returns400()
    {
        var response = await _client.RegisterAsync(ApiClientExtensions.NewEmail(), new string('a', 129));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
