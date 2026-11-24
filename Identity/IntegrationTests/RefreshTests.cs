using System.Net;
using System.Net.Http.Json;
using ChatFusion.Identity.Api.Contracts;
using ChatFusion.Identity.IntegrationTests.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ChatFusion.Identity.IntegrationTests;

public sealed class RefreshTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Refresh_ValidToken_RotatesTokensInSameSession()
    {
        var (_, tokens) = await _client.RegisterAndLoginAsync();

        var response = await _client.RefreshAsync(tokens.RefreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        refreshed.RefreshToken.Should().NotBe(tokens.RefreshToken);
        SessionIdOf(refreshed).Should().Be(SessionIdOf(tokens));

        (await _client.RefreshAsync(refreshed.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_UsedTokenWithinGracePeriod_Returns409AndKeepsSession()
    {
        var (_, tokens) = await _client.RegisterAndLoginAsync();
        var refreshed = await RefreshSuccessfullyAsync(tokens.RefreshToken);

        var repeated = await _client.RefreshAsync(tokens.RefreshToken);

        repeated.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await repeated.ReadProblemCodeAsync()).Should().Be("RefreshTokenAlreadyRotated");
        (await _client.RefreshAsync(refreshed.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_UsedTokenAfterGracePeriod_RevokesWholeSession()
    {
        var (_, tokens) = await _client.RegisterAndLoginAsync();
        var refreshed = await RefreshSuccessfullyAsync(tokens.RefreshToken);

        factory.Time.Advance(TimeSpan.FromSeconds(31));
        var reused = await _client.RefreshAsync(tokens.RefreshToken);

        reused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.RefreshAsync(refreshed.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ConcurrentRequestsWithSameToken_OnlyOneRotates()
    {
        var (_, tokens) = await _client.RegisterAndLoginAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _client.RefreshAsync(tokens.RefreshToken)));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(4);
    }

    [Fact]
    public async Task Refresh_AfterInactivityTimeout_Returns401()
    {
        var (_, tokens) = await _client.RegisterAndLoginAsync();

        factory.Time.Advance(TimeSpan.FromDays(7) + TimeSpan.FromMinutes(1));

        (await _client.RefreshAsync(tokens.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_AfterAbsoluteLifetime_Returns401EvenWithRegularUse()
    {
        var (_, tokens) = await _client.RegisterAndLoginAsync();
        var current = tokens;

        // Обновление каждые 6 дней укладывается в таймаут неактивности (7 дней), но не продлевает сессию дольше 30 дней
        for (var day = 0; day < 24; day += 6)
        {
            factory.Time.Advance(TimeSpan.FromDays(6));
            current = await RefreshSuccessfullyAsync(current.RefreshToken);
        }

        current.RefreshTokenExpiresAt.Should().BeBefore(factory.Time.GetUtcNow().AddDays(7));
        factory.Time.Advance(TimeSpan.FromDays(6) + TimeSpan.FromMinutes(1));

        (await _client.RefreshAsync(current.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-token")]
    public async Task Refresh_InvalidToken_Returns401(string? refreshToken)
    {
        var response = await _client.RefreshAsync(refreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_TooLongToken_Returns400()
    {
        var response = await _client.RefreshAsync(new string('a', 500));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<TokenResponse> RefreshSuccessfullyAsync(string? refreshToken)
    {
        var response = await _client.RefreshAsync(refreshToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    private static string SessionIdOf(TokenResponse tokens) => new JsonWebToken(tokens.AccessToken).GetClaim("sid").Value;
}
