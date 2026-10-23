namespace PandaBear.VaultManager.Tool.Tests;

public sealed class PasswordReaderTests
{
    [Fact]
    public void Read_returns_typed_text_without_echoing_it()
    {
        using var output = new StringWriter();
        var reader = new PasswordReader(ConsoleKeys.Typing("secret pw"), output);

        var password = reader.Read("Password");

        password.ShouldBe("secret pw");
        output.ToString().ShouldNotContain("secret", StringComparison.Ordinal);
    }

    [Fact]
    public void Read_applies_backspace()
    {
        using var output = new StringWriter();
        var reader = new PasswordReader(ConsoleKeys.Typing("abx\bc"), output);

        reader.Read("Password").ShouldBe("abc");
    }

    [Fact]
    public void ReadNew_retries_until_entries_match()
    {
        using var output = new StringWriter();
        var reader = new PasswordReader(ConsoleKeys.Typing("long enough one", "different entry", "long enough two", "long enough two"), output);

        var password = reader.ReadNew("File password");

        password.ShouldBe("long enough two");
        output.ToString().ShouldContain("do not match", StringComparison.Ordinal);
    }

    [Fact]
    public void ReadNew_rejects_short_passwords()
    {
        using var output = new StringWriter();
        var reader = new PasswordReader(ConsoleKeys.Typing("short", "short", "short"), output);

        var action = () => reader.ReadNew("File password");

        action.ShouldThrow<InvalidOperationException>();
        output.ToString().ShouldContain("at least 12", StringComparison.Ordinal);
    }
}
