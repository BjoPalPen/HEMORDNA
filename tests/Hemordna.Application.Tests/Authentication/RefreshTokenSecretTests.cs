using System.Text.RegularExpressions;
using Hemordna.Application.Authentication;
using Hemordna.Domain.Authentication;

namespace Hemordna.Application.Tests.Authentication;

public partial class RefreshTokenSecretTests
{
    [Fact]
    public void Generate_returns_a_non_blank_value()
        => Assert.False(string.IsNullOrWhiteSpace(RefreshTokenSecret.Generate()));

    [Fact]
    public void Generate_returns_a_different_value_every_time()
    {
        var first = RefreshTokenSecret.Generate();
        var second = RefreshTokenSecret.Generate();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Generate_returns_a_url_safe_value()
        => Assert.Matches(UrlSafeCharacters(), RefreshTokenSecret.Generate());

    [Fact]
    public void Hash_returns_a_lowercase_hex_string_of_the_expected_length()
    {
        var hash = RefreshTokenSecret.Hash("some-secret-value");

        Assert.Equal(RefreshToken.TokenHashLength, hash.Length);
        Assert.Matches(LowercaseHex(), hash);
    }

    [Fact]
    public void Hash_is_deterministic()
    {
        var first = RefreshTokenSecret.Hash("the-same-secret");
        var second = RefreshTokenSecret.Hash("the-same-secret");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Hash_of_different_secrets_differ()
    {
        var first = RefreshTokenSecret.Hash("secret-one");
        var second = RefreshTokenSecret.Hash("secret-two");

        Assert.NotEqual(first, second);
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex UrlSafeCharacters();

    [GeneratedRegex("^[0-9a-f]+$")]
    private static partial Regex LowercaseHex();
}
