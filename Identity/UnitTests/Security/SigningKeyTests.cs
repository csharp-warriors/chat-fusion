using System.Security.Cryptography;
using ChatFusion.Identity.Api.Infrastructure.Configuration;
using ChatFusion.Identity.Api.Infrastructure.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChatFusion.Identity.UnitTests.Security;

public sealed class SigningKeyTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "chatfusion-identity-tests", Guid.NewGuid().ToString("N"));

    public SigningKeyTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void LoadFromPemFile_GeneratedKey_HasStableKeyId()
    {
        var path = WriteFile("key.pem", SigningKey.GeneratePrivateKeyPem());

        var first = SigningKey.LoadFromPemFile(path, requirePrivateKey: true);
        var second = SigningKey.LoadFromPemFile(path, requirePrivateKey: true);

        first.KeyId.Should().NotBeNullOrEmpty();
        first.KeyId.Should().Be(second.KeyId);
        first.HasPrivateKey.Should().BeTrue();
    }

    [Fact]
    public void LoadFromPemFile_MissingFile_Throws()
    {
        var act = () => SigningKey.LoadFromPemFile(Path.Combine(_directory, "missing.pem"), requirePrivateKey: true);

        act.Should().Throw<InvalidOperationException>().WithMessage("*не найден*");
    }

    [Fact]
    public void LoadFromPemFile_NotPem_Throws()
    {
        var path = WriteFile("broken.pem", "это не ключ");

        var act = () => SigningKey.LoadFromPemFile(path, requirePrivateKey: true);

        act.Should().Throw<InvalidOperationException>().WithMessage("*PEM*");
    }

    [Fact]
    public void LoadFromPemFile_PublicKeyOnly_RejectedForSigningButAcceptedForValidation()
    {
        using var rsa = RSA.Create(2048);
        var path = WriteFile("public.pem", rsa.ExportSubjectPublicKeyInfoPem());

        var asSigningKey = () => SigningKey.LoadFromPemFile(path, requirePrivateKey: true);
        asSigningKey.Should().Throw<InvalidOperationException>().WithMessage("*закрытый*");

        var validationKey = SigningKey.LoadFromPemFile(path, requirePrivateKey: false);
        validationKey.HasPrivateKey.Should().BeFalse();
        validationKey.Invoking(k => k.CreateSigningCredentials()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LoadFromPemFile_ShortKey_Throws()
    {
        using var rsa = RSA.Create(1024);
        var path = WriteFile("short.pem", rsa.ExportPkcs8PrivateKeyPem());

        var act = () => SigningKey.LoadFromPemFile(path, requirePrivateKey: true);

        act.Should().Throw<InvalidOperationException>().WithMessage("*2048*");
    }

    [Fact]
    public void ToPublicJwk_ContainsOnlyPublicParameters()
    {
        var key = SigningKey.Generate();

        var jwk = key.ToPublicJwk();

        jwk.Kty.Should().Be("RSA");
        jwk.Use.Should().Be("sig");
        jwk.Alg.Should().Be("RS256");
        jwk.Kid.Should().Be(key.KeyId);
        jwk.N.Should().NotBeNullOrEmpty();
        jwk.E.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Provider_WithoutKeyOutsideDevelopment_Throws()
    {
        var act = () => CreateProvider(new JwtOptions { Issuer = "i", Audience = "a" }, Environments.Production);

        act.Should().Throw<InvalidOperationException>().WithMessage("*SigningKeyPath*");
    }

    [Fact]
    public void Provider_WithoutKeyInDevelopment_GeneratesTemporaryKey()
    {
        var provider = CreateProvider(new JwtOptions { Issuer = "i", Audience = "a" }, Environments.Development);

        provider.Current.HasPrivateKey.Should().BeTrue();
        provider.ValidationKeys.Should().ContainSingle();
    }

    [Fact]
    public void Provider_WithPreviousKey_PublishesBothKeys()
    {
        var current = WriteFile("current.pem", SigningKey.GeneratePrivateKeyPem());
        var previous = WriteFile("previous.pem", SigningKey.GeneratePrivateKeyPem());

        var provider = CreateProvider(
            new JwtOptions { Issuer = "i", Audience = "a", SigningKeyPath = current, PreviousSigningKeyPath = previous },
            Environments.Production);

        provider.ValidationKeys.Should().HaveCount(2);
        provider.ValidationKeys[0].Should().BeSameAs(provider.Current);
        provider.ValidationKeys[1].KeyId.Should().NotBe(provider.Current.KeyId);
    }

    private static SigningKeyProvider CreateProvider(JwtOptions options, string environmentName) =>
        new(
            Options.Create(options),
            new HostingEnvironment { EnvironmentName = environmentName },
            NullLogger<SigningKeyProvider>.Instance);

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }
}
