using ChatFusion.Identity.Core.Accounts;
using ChatFusion.Identity.Api.Infrastructure.Security;

namespace ChatFusion.Identity.UnitTests.Security;

public sealed class EmailNormalizerTests
{
    private readonly EmailNormalizer _normalizer = new();

    [Theory]
    [InlineData("user@example.com", "user@example.com", "USER@EXAMPLE.COM")]
    [InlineData("  User@Example.COM  ", "User@Example.COM", "USER@EXAMPLE.COM")]
    [InlineData("first.last+tag@sub.example.org", "first.last+tag@sub.example.org", "FIRST.LAST+TAG@SUB.EXAMPLE.ORG")]
    public void TryNormalize_ValidEmail_TrimsAndUppercases(string input, string expectedEmail, string expectedNormalized)
    {
        _normalizer.TryNormalize(input, out var email, out var normalized).Should().BeTrue();

        email.Should().Be(expectedEmail);
        normalized.Should().Be(expectedNormalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("user@")]
    [InlineData("@example.com")]
    [InlineData("user name@example.com")]
    [InlineData("User <user@example.com>")]
    public void TryNormalize_InvalidEmail_ReturnsFalse(string? input)
    {
        _normalizer.TryNormalize(input, out var email, out var normalized).Should().BeFalse();

        email.Should().BeNull();
        normalized.Should().BeNull();
    }

    [Fact]
    public void TryNormalize_TooLongEmail_ReturnsFalse()
    {
        var input = new string('a', EmailNormalizer.MaxLength) + "@example.com";

        _normalizer.TryNormalize(input, out _, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("1234567", false)]
    [InlineData("12345678", true)]
    public void PasswordPolicy_ChecksLength(string? password, bool expected)
    {
        PasswordPolicy.IsSatisfiedBy(password).Should().Be(expected);
    }

    [Fact]
    public void PasswordPolicy_RejectsTooLongPassword()
    {
        PasswordPolicy.IsSatisfiedBy(new string('a', PasswordPolicy.MaxLength)).Should().BeTrue();
        PasswordPolicy.IsSatisfiedBy(new string('a', PasswordPolicy.MaxLength + 1)).Should().BeFalse();
    }
}
