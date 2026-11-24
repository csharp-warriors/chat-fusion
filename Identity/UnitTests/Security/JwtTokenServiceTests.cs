using ChatFusion.Identity.Api.Infrastructure.Configuration;
using ChatFusion.Identity.Api.Infrastructure.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ChatFusion.Identity.UnitTests.Security;

public sealed class JwtTokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);

    private readonly JwtOptions _options = new()
    {
        Issuer = "https://identity.test",
        Audience = "chatfusion-api",
        AccessTokenLifetime = TimeSpan.FromMinutes(15),
    };

    private readonly SigningKeyProvider _keys;
    private readonly JwtTokenService _service;

    public JwtTokenServiceTests()
    {
        _keys = new SigningKeyProvider(
            Options.Create(_options),
            new HostingEnvironment { EnvironmentName = Environments.Development },
            NullLogger<SigningKeyProvider>.Instance);

        _service = new JwtTokenService(_keys, Options.Create(_options), new FakeTimeProvider(Now));
    }

    [Fact]
    public void CreateAccessToken_ContainsExpectedClaims()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var token = _service.CreateAccessToken(userId, sessionId, ["admin", "user"]);
        var jwt = new JsonWebToken(token.Token);

        jwt.Issuer.Should().Be(_options.Issuer);
        jwt.Audiences.Should().ContainSingle().Which.Should().Be(_options.Audience);
        jwt.Subject.Should().Be(userId.ToString());
        jwt.GetClaim("sid").Value.Should().Be(sessionId.ToString());
        jwt.Id.Should().NotBeNullOrEmpty();
        jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value).Should().BeEquivalentTo(["admin", "user"]);
    }

    [Fact]
    public void CreateAccessToken_HasLifetimeFromSettings()
    {
        var token = _service.CreateAccessToken(Guid.NewGuid(), Guid.NewGuid(), ["user"]);
        var jwt = new JsonWebToken(token.Token);

        token.ExpiresAt.Should().Be(Now.AddMinutes(15));
        jwt.ValidTo.Should().Be(Now.AddMinutes(15).UtcDateTime);
        jwt.IssuedAt.Should().Be(Now.UtcDateTime);
    }

    [Fact]
    public void CreateAccessToken_IsSignedWithCurrentKey()
    {
        var token = _service.CreateAccessToken(Guid.NewGuid(), Guid.NewGuid(), ["user"]);
        var jwt = new JsonWebToken(token.Token);

        jwt.Alg.Should().Be(SecurityAlgorithms.RsaSha256);
        jwt.Kid.Should().Be(_keys.Current.KeyId);
    }

    [Fact]
    public async Task CreateAccessToken_ValidatesWithPublicKey()
    {
        var token = _service.CreateAccessToken(Guid.NewGuid(), Guid.NewGuid(), ["user"]);
        var publicKey = new RsaSecurityKey(_keys.Current.SecurityKey.Rsa.ExportParameters(includePrivateParameters: false))
        {
            KeyId = _keys.Current.KeyId,
        };

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = publicKey,
            ValidateLifetime = false,
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateAccessToken_DoesNotContainPersonalData()
    {
        var token = _service.CreateAccessToken(Guid.NewGuid(), Guid.NewGuid(), ["user"]);
        var jwt = new JsonWebToken(token.Token);

        jwt.Claims.Select(c => c.Type).Should().NotContain(["email", "name", "unique_name"]);
    }

    [Fact]
    public void RefreshTokenGenerator_CreatesUniqueTokensWithStableHash()
    {
        var first = RefreshTokenGenerator.Generate();
        var second = RefreshTokenGenerator.Generate();

        first.Should().NotBe(second);
        first.Length.Should().BeLessThanOrEqualTo(RefreshTokenGenerator.MaxTokenLength);
        RefreshTokenGenerator.Hash(first).Should().Equal(RefreshTokenGenerator.Hash(first));
        RefreshTokenGenerator.Hash(first).Should().HaveCount(32);
    }
}
