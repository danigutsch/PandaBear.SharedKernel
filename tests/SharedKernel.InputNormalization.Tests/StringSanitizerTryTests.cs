namespace PandaBear.InputNormalization.Tests;

/// <summary>
/// Tests the non-throwing sanitizers: malformed UTF-16, such as a lone surrogate from a truncated or hostile form field, is reported as
/// <see langword="false"/> instead of the <see cref="ArgumentException"/> that Unicode normalization throws, and valid text gives exactly
/// what the throwing methods give.
/// </summary>
public sealed class StringSanitizerTryTests
{
    [Theory]
    [MemberData(nameof(Malformed))]
    public void Malformed_text_is_refused_without_an_exception(string because, string value)
    {
        // Act
        var sanitized = StringSanitizer.TrySanitize(value, out var result);
        var notes = StringSanitizer.TrySanitizeNotes(value, out var notesResult);
        var key = StringSanitizer.TryNormalizeKey(value, out var keyResult);

        // Assert
        sanitized.ShouldBeFalse(because);
        result.ShouldBeNull(because);
        notes.ShouldBeFalse(because);
        notesResult.ShouldBeNull(because);
        key.ShouldBeFalse(because);
        keyResult.ShouldBeNull(because);
    }

    [Theory]
    [MemberData(nameof(Valid))]
    public void Valid_text_gives_what_the_throwing_methods_give(string because, string value)
    {
        // Act
        var sanitized = StringSanitizer.TrySanitize(value, out var result);
        var notes = StringSanitizer.TrySanitizeNotes(value, out var notesResult);
        var key = StringSanitizer.TryNormalizeKey(value, out var keyResult);

        // Assert
        sanitized.ShouldBeTrue(because);
        result.ShouldBe(StringSanitizer.Sanitize(value), because);
        notes.ShouldBeTrue(because);
        notesResult.ShouldBe(StringSanitizer.SanitizeNotes(value), because);
        key.ShouldBeTrue(because);
        keyResult.ShouldBe(StringSanitizer.NormalizeKey(value), because);
    }

    [Theory]
    [MemberData(nameof(Malformed))]
    public void The_throwing_methods_still_throw_for_malformed_text(string because, string value)
    {
        // Act
        Action act = () => StringSanitizer.Sanitize(value);

        // Assert
        act.ShouldThrow<ArgumentException>(because);
    }

    [Fact]
    public void Null_is_valid_and_stays_null()
    {
        // Act
        var sanitized = StringSanitizer.TrySanitize(null, out var result);
        var notes = StringSanitizer.TrySanitizeNotes(null, out var notesResult);

        // Assert
        sanitized.ShouldBeTrue();
        result.ShouldBeNull();
        notes.ShouldBeTrue();
        notesResult.ShouldBeNull();
    }

    [Fact]
    public void A_null_key_is_refused()
    {
        // Act
        Action act = () => _ = StringSanitizer.TryNormalizeKey(null!, out _);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    /// <summary>Gets text that is not well-formed UTF-16.</summary>
    public static TheoryData<string, string> Malformed => new()
    {
        { "a lone high surrogate", "tour\uD800" },
        { "a lone low surrogate", "\uDC00tour" },
        { "two high surrogates", "\uD83D\uD83D" },
        { "a reversed surrogate pair", "\uDE00\uD83D" },
    };

    /// <summary>Gets well-formed text, including a supplementary character and text that normalization changes.</summary>
    public static TheoryData<string, string> Valid => new()
    {
        { "plain text", "  Lisbon   tour " },
        { "an emoji pair", "tour 😀" },
        { "a decomposed accent", "Café" },
        { "control characters", "a\u0001b" },
        { "an empty string", string.Empty },
    };
}
