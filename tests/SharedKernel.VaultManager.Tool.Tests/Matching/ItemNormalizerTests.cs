using PandaBear.VaultManager.Tool.Matching;

namespace PandaBear.VaultManager.Tool.Tests.Matching;

public sealed class ItemNormalizerTests
{
    [Theory]
    [InlineData("https://www.Example.com:8443/login?next=1#top", "example.com")]
    [InlineData("example.com/login", "example.com")]
    [InlineData("  EXAMPLE.com  ", "example.com")]
    [InlineData("localhost:3000/app", "localhost")]
    [InlineData("http://user@192.168.1.10:8080/admin", "192.168.1.10")]
    [InlineData("https://[2001:db8::1]:443/", "[2001:db8::1]")]
    [InlineData("androidapp://com.Example.App", "androidapp://com.example.app")]
    public void Host_normalizes_web_addresses_and_keeps_other_schemes_whole(string value, string expected)
    {
        ItemNormalizer.Host(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://")]
    public void Host_of_blank_address_is_null(string value)
    {
        ItemNormalizer.Host(value).ShouldBeNull();
    }

    [Fact]
    public void Username_title_and_notes_are_normalized()
    {
        ItemNormalizer.Username("  Alice@Example.COM ").ShouldBe("alice@example.com");
        ItemNormalizer.Title("  Home \t  WiFi\n").ShouldBe("home wifi");
        ItemNormalizer.Notes("one\r\ntwo\rthree  \n\n").ShouldBe("one\ntwo\nthree");
    }

    [Theory]
    [InlineData("otpauth://totp/Example:alice?secret=jbsw%20y3dp-ehpk3pxp%3D%3D&issuer=Example", "JBSWY3DPEHPK3PXP")]
    [InlineData("jbsw y3dp-ehpk 3pxp==", "JBSWY3DPEHPK3PXP")]
    [InlineData("steam://ab-cd ef", "STEAM://AB-CD EF")]
    [InlineData("otpauth://totp/no-secret", "OTPAUTH://TOTP/NOSECRET")]
    public void Totp_comparison_key_uses_secret_and_ignores_formatting(string value, string expected)
    {
        ItemNormalizer.Totp(value).ShouldBe(expected);
    }
}
