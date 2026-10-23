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
    [InlineData("HTTPS://Example.com/x", "example.com")]
    [InlineData("ftp://Files.Example.com/pub", "ftp://files.example.com/pub")]
    [InlineData("mailto:Alice@Example.com", "mailto:alice@example.com")]
    [InlineData("web+app:open", "web+app:open")]
    [InlineData("host:80x", "host:80x")]
    [InlineData("example.com:8443", "example.com")]
    [InlineData("example.com:8443?next=1", "example.com")]
    [InlineData("1password://open", "1password")]
    public void Host_normalizes_web_addresses_and_keeps_other_schemes_whole(string value, string expected)
    {
        // Act
        var host = ItemNormalizer.Host(value);

        // Assert
        host.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://")]
    public void Host_of_blank_address_is_null(string value)
    {
        // Act
        var host = ItemNormalizer.Host(value);

        // Assert
        host.ShouldBeNull();
    }

    [Fact]
    public void Username_title_and_notes_are_normalized()
    {
        // Act
        var username = ItemNormalizer.Username("  Alice@Example.COM ");
        var title = ItemNormalizer.Title("  Home \t  WiFi\n");
        var notes = ItemNormalizer.Notes("one\r\ntwo\rthree  \n\n");

        // Assert
        username.ShouldBe("alice@example.com");
        title.ShouldBe("home wifi");
        notes.ShouldBe("one\ntwo\nthree");
    }

    [Fact]
    public void Title_equates_unicode_forms_and_drops_control_characters()
    {
        // Act
        string decomposed = ItemNormalizer.Title("Café \u0007Wi\u0000Fi");
        string composed = ItemNormalizer.Title("Café WiFi");

        // Assert
        decomposed.ShouldBe("café wifi");
        composed.ShouldBe("café wifi");
    }

    [Theory]
    [InlineData("otpauth://totp/Example:alice?secret=jbsw%20y3dp-ehpk3pxp%3D%3D&issuer=Example", "JBSWY3DPEHPK3PXP")]
    [InlineData("jbsw y3dp-ehpk 3pxp==", "JBSWY3DPEHPK3PXP")]
    [InlineData("steam://ab-cd ef", "STEAM://AB-CD EF")]
    [InlineData("otpauth://totp/no-secret", "OTPAUTH://TOTP/NOSECRET")]
    public void Totp_comparison_key_uses_secret_and_ignores_formatting(string value, string expected)
    {
        // Act
        var key = ItemNormalizer.Totp(value);

        // Assert
        key.ShouldBe(expected);
    }

    [Theory]
    [InlineData("otpauth://totp/Example:alice?secret=JBSWY3DPEHPK3PXP&issuer=Example", "otpauth://totp/Other:bob?secret=JBSWY3DPEHPK3PXP")]
    [InlineData("otpauth://totp/Other?secret=JBSWY3DPEHPK3PXP&algorithm=sha1&digits=6&period=30", "jbsw y3dp ehpk 3pxp")]
    [InlineData("steam://jbsw y3dp ehpk 3pxp", "STEAM://JBSWY3DPEHPK3PXP")]
    public void Totp_keys_of_values_that_generate_the_same_codes_are_equal(string first, string second)
    {
        // Act
        string firstKey = ItemNormalizer.Totp(first);
        string secondKey = ItemNormalizer.Totp(second);

        // Assert
        firstKey.ShouldBe(secondKey);
    }

    [Theory]
    [InlineData("otpauth://totp/Example?secret=JBSWY3DPEHPK3PXP&digits=8", "JBSWY3DPEHPK3PXP")]
    [InlineData("otpauth://totp/Example?secret=JBSWY3DPEHPK3PXP&period=60", "JBSWY3DPEHPK3PXP")]
    [InlineData("otpauth://totp/Example?secret=JBSWY3DPEHPK3PXP&algorithm=SHA256", "JBSWY3DPEHPK3PXP")]
    [InlineData("steam://JBSWY3DPEHPK3PXP", "JBSWY3DPEHPK3PXP")]
    public void Totp_keys_differ_when_code_settings_differ(string first, string second)
    {
        // Act
        string firstKey = ItemNormalizer.Totp(first);
        string secondKey = ItemNormalizer.Totp(second);

        // Assert
        firstKey.ShouldNotBe(secondKey);
    }
}
