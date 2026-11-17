namespace PandaBear.Secrets.Tests;

/// <summary>Tests that an identity reference is a file path and never key material.</summary>
[Trait("Capability", "Secrets")]
public sealed class AgeIdentityRefTests
{
    [Fact]
    public void An_absolute_path_is_kept()
    {
        // Act
        var identity = AgeIdentityRef.Create("/home/user/.config/age/key.txt").ShouldBeSuccess();

        // Assert
        identity.Path.ShouldBe("/home/user/.config/age/key.txt");
        identity.ToString().ShouldBe("/home/user/.config/age/key.txt");
    }

    [Theory]
    [InlineData(FakeKeys.Secret)]
    [InlineData(FakeKeys.LowercaseSecret)]
    [InlineData("# created: 2026\n" + FakeKeys.Secret)]
    [InlineData("/tmp/AGE-SECRET-KEY-1QQQQQ")]
    [InlineData("AGE-PLUGIN-YUBIKEY-1QQQQQQQQQQQQQQQQQ")]
    public void Text_that_looks_like_key_material_is_rejected_with_a_message_that_says_to_pass_a_file(string material)
    {
        // Act
        var result = AgeIdentityRef.Create(material);

        // Assert
        var message = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["identity"].ShouldHaveSingleItem();
        message.ShouldContain("Pass the path of an identity file, never the key itself.", StringComparison.Ordinal);
        message.ShouldNotContain("QQQQ", StringComparison.Ordinal);
        result.ErrorDetails.Detail.ShouldNotContain("QQQQ", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/key.txt")]
    [InlineData("key.txt")]
    [InlineData("/home/user/")]
    [InlineData("/home/../etc/key")]
    [InlineData("/home/user/key\n.txt")]
    [InlineData("/home/user/key\0.txt")]
    public void An_invalid_path_is_rejected_without_echoing_it(string? input)
    {
        // Act
        var result = AgeIdentityRef.Create(input);

        // Assert
        var message = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["identity"].ShouldHaveSingleItem();
        if (!string.IsNullOrEmpty(input))
        {
            message.ShouldNotContain(input, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_path_longer_than_the_limit_is_rejected()
    {
        // Act
        var result = AgeIdentityRef.Create("/" + new string('a', AgeIdentityRef.MaxLength));

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Equal_paths_are_equal()
    {
        // Assert
        AgeIdentityRef.Create("/k").ShouldBeSuccess().ShouldBe(AgeIdentityRef.Create("/k").ShouldBeSuccess());
    }
}
