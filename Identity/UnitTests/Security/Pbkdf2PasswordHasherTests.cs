using ChatFusion.Identity.Core.Accounts;
using ChatFusion.Identity.Api.Infrastructure.Configuration;
using ChatFusion.Identity.Api.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace ChatFusion.Identity.UnitTests.Security;

public sealed class Pbkdf2PasswordHasherTests
{
    private const string Password = "Str0ng-Passw0rd";

    private static Pbkdf2PasswordHasher CreateHasher(int iterations = 10_000) =>
        new(Options.Create(new PasswordHashingOptions { Iterations = iterations }));

    [Fact]
    public void Verify_CorrectPassword_ReturnsSuccess()
    {
        var hasher = CreateHasher();
        var hash = hasher.Hash(Password);

        hasher.Verify(hash, Password).Should().Be(PasswordCheckResult.Success);
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFailed()
    {
        var hasher = CreateHasher();
        var hash = hasher.Hash(Password);

        hasher.Verify(hash, Password + "x").Should().Be(PasswordCheckResult.Failed);
    }

    [Fact]
    public void Hash_HasVersionedFormat()
    {
        var hash = CreateHasher(12_345).Hash(Password);

        var parts = hash.Split('$');
        parts.Should().HaveCount(4);
        parts[0].Should().Be("v1");
        parts[1].Should().Be("12345");
        Convert.FromBase64String(parts[2]).Should().HaveCount(16);
        Convert.FromBase64String(parts[3]).Should().HaveCount(32);
    }

    [Fact]
    public void Hash_SamePassword_UsesDifferentSalt()
    {
        var hasher = CreateHasher();

        hasher.Hash(Password).Should().NotBe(hasher.Hash(Password));
    }

    [Fact]
    public void Verify_HashWithFewerIterations_RequestsRehash()
    {
        var oldHash = CreateHasher(10_000).Hash(Password);

        CreateHasher(20_000).Verify(oldHash, Password).Should().Be(PasswordCheckResult.SuccessRehashNeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain-text")]
    [InlineData("v2$10000$c2FsdA==$aGFzaA==")]
    [InlineData("v1$abc$c2FsdA==$aGFzaA==")]
    [InlineData("v1$0$c2FsdA==$aGFzaA==")]
    [InlineData("v1$99999999$c2FsdA==$aGFzaA==")]
    [InlineData("v1$10000$not-base64!$aGFzaA==")]
    [InlineData("v1$10000$$aGFzaA==")]
    [InlineData("v1$10000$c2FsdA==")]
    public void Verify_MalformedHash_ReturnsFailed(string malformedHash)
    {
        CreateHasher().Verify(malformedHash, Password).Should().Be(PasswordCheckResult.Failed);
    }

    [Fact]
    public void SimulateVerification_DoesNotThrow()
    {
        var act = () => CreateHasher().SimulateVerification(Password);

        act.Should().NotThrow();
    }
}
