using PandaBear.Results;

namespace PandaBear.UploadSanitizing.Tests.Fonts;

/// <summary>Assertions on font sanitizer results.</summary>
internal static class FontAssertions
{
    /// <summary>Asserts an invalid result with exactly one message on the field.</summary>
    /// <param name="result">The result.</param>
    /// <param name="message">The expected message.</param>
    /// <param name="field">The expected field.</param>
    internal static void ShouldBeRejected(this Result<SanitizedFont> result, string message, string field = "file") =>
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid).ShouldHaveValidationError(field, message);

    /// <summary>Asserts a success whose bytes are the input unchanged, or an invalid result with one of the sanitizer's messages.</summary>
    /// <param name="result">The result.</param>
    /// <param name="input">The input.</param>
    internal static void ShouldBeSuccessOrRejected(this Result<SanitizedFont> result, ReadOnlySpan<byte> input)
    {
        if (result.IsSuccess)
        {
            result.Value.Bytes.Span.SequenceEqual(input).ShouldBeTrue();
            result.Value.Format.ShouldBe(FontFormat.Woff2);
            return;
        }

        var messages = result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid).ValidationErrors.ShouldNotBeNull()["file"];
        messages.Count.ShouldBe(1);
        messages[0].ShouldBeOneOf(FontSanitizer.NotAFontMessage, FontSanitizer.MalformedMessage, FontSanitizer.TooLargeMessage);
    }
}
