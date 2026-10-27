namespace PandaBear.VaultManager.Tool.Tests;

/// <summary>Tests that <see cref="PasswordReader"/> reads hidden console input, handles backspace, and enforces new-password rules.</summary>
public sealed class PasswordReaderTests
{
    /// <summary>Read returns the typed text "secret pw" and writes none of it to the output.</summary>
    [Fact]
    public void Read_returns_typed_text_without_echoing_it()
    {
        // Arrange
        using var output = new StringWriter();
        var reader = new PasswordReader(ConsoleKeys.Typing("secret pw"), output);

        // Act
        var password = reader.Read("Password");

        // Assert
        password.ShouldBe("secret pw");
        output.ToString().ShouldNotContain("secret", StringComparison.Ordinal);
    }

    /// <summary>Typing "abx", a backspace, then "c" yields "abc".</summary>
    [Fact]
    public void Read_applies_backspace()
    {
        // Arrange
        using var output = new StringWriter();
        var reader = new PasswordReader(ConsoleKeys.Typing("abx\bc"), output);

        // Act
        var password = reader.Read("Password");

        // Assert
        password.ShouldBe("abc");
    }

    /// <summary>When the first confirmation differs, ReadNew prints a "do not match" message, asks again, and returns the second matching entry.</summary>
    [Fact]
    public void ReadNew_retries_until_entries_match()
    {
        // Arrange
        using var output = new StringWriter();
        var reader = new PasswordReader(ConsoleKeys.Typing("long enough one", "different entry", "long enough two", "long enough two"), output);

        // Act
        var password = reader.ReadNew("File password");

        // Assert
        password.ShouldBe("long enough two");
        output.ToString().ShouldContain("do not match", StringComparison.Ordinal);
    }

    /// <summary>Repeatedly entering the five-character "short" makes ReadNew throw <see cref="InvalidOperationException"/> after printing an "at least 12" requirement.</summary>
    [Fact]
    public void ReadNew_rejects_short_passwords()
    {
        // Arrange
        using var output = new StringWriter();
        var reader = new PasswordReader(ConsoleKeys.Typing("short", "short", "short"), output);
        var action = () => reader.ReadNew("File password");

        // Act
        action.ShouldThrow<InvalidOperationException>();

        // Assert
        output.ToString().ShouldContain("at least 12", StringComparison.Ordinal);
    }
}
