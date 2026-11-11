using PandaBear.Results;

namespace PandaBear.ImageSanitizing.Tests;

/// <summary>Assertions on sanitizer results.</summary>
internal static class SanitizerAssertions
{
    /// <summary>Asserts an invalid result with exactly one message on the field.</summary>
    /// <param name="result">The result.</param>
    /// <param name="message">The expected message.</param>
    /// <param name="field">The expected field.</param>
    internal static void ShouldBeRejected(this Result<SanitizedImage> result, string message, string field = "file") =>
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid).ShouldHaveValidationError(field, message);

    /// <summary>Asserts a success or an invalid result with one of the sanitizer's messages, and a consistent image on success.</summary>
    /// <param name="result">The result.</param>
    /// <param name="inputLength">The length of the input.</param>
    internal static void ShouldBeSuccessOrRejected(this Result<SanitizedImage> result, int inputLength)
    {
        if (result.IsSuccess)
        {
            var image = result.Value;
            image.Bytes.Length.ShouldBeLessThanOrEqualTo(inputLength);
            image.Width.ShouldBeInRange(1, ImageSanitizerOptions.Default.MaxDimension);
            image.Height.ShouldBeInRange(1, ImageSanitizerOptions.Default.MaxDimension);
            return;
        }

        var messages = result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid).ValidationErrors.ShouldNotBeNull()["file"];
        messages.Count.ShouldBe(1);
        messages[0].ShouldBeOneOf(
            ImageSanitizer.NotAnImageMessage,
            ImageSanitizer.MalformedMessage,
            ImageSanitizer.TooLargeMessage,
            ImageSanitizer.DimensionsTooLargeMessage);
    }
}
