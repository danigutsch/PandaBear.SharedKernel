using SharedKernel.Testing;

namespace SharedKernel.UploadSanitizing.Tests.Images;

/// <summary>
/// Fuzz-style tests: truncations, single-byte mutations and random bytes behind each signature never throw, and every success is a
/// consistent image no longer than its input.
/// </summary>
[TestScope(TestScope.Unit)]
public sealed class ImageSanitizerRobustnessTests
{
    private static readonly byte[] MutationValues = [0x00, 0x01, 0x7F, 0x80, 0xFF];

    [Theory]
    [MemberData(nameof(Samples))]
    public void Every_truncation_of_a_valid_image_is_answered_with_a_result(string name, byte[] file)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(file);
        var answered = 0;

        // Act
        for (var length = 0; length <= file.Length; length++)
        {
            var prefix = file.AsSpan(0, length);
            ImageSanitizer.Sanitize(prefix, ImageSanitizerOptions.Default).ShouldBeSuccessOrRejected(length);
            ImageSanitizer.SanitizeInPlace(prefix.ToArray(), ImageSanitizerOptions.Default).ShouldBeSuccessOrRejected(length);
            answered++;
        }

        // Assert
        answered.ShouldBe(file.Length + 1, name);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Every_single_byte_change_of_a_valid_image_is_answered_with_a_result(string name, byte[] file)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(file);
        var answered = 0;

        // Act
        for (var index = 0; index < file.Length; index++)
        {
            foreach (var value in MutationValues)
            {
                var mutated = ImageSamples.Corrupt(file, index, value);
                ImageSanitizer.SanitizeInPlace(mutated, ImageSanitizerOptions.Default).ShouldBeSuccessOrRejected(file.Length);
                answered++;
            }
        }

        // Assert
        answered.ShouldBe(file.Length * MutationValues.Length, name);
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF })]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46 })]
    [InlineData(new byte[0])]
    public void Random_bytes_behind_a_signature_are_answered_with_a_result(byte[] signature)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(signature);
        var random = new Random(20261111);
        var answered = 0;

        // Act
        for (var iteration = 0; iteration < 2000; iteration++)
        {
            var file = new byte[signature.Length + random.Next(0, 300)];
            random.NextBytes(file);
            signature.CopyTo(file, 0);
            if (signature.Length == 4 && file.Length >= 12)
            {
                // A RIFF header of type WEBP with the size of the file, so the chunk parser is reached.
                BitConverter.GetBytes(file.Length - 8).CopyTo(file, 4);
                "WEBP"u8.CopyTo(file.AsSpan(8));
            }

            ImageSanitizer.SanitizeInPlace(file, ImageSanitizerOptions.Default).ShouldBeSuccessOrRejected(file.Length);
            answered++;
        }

        // Assert
        answered.ShouldBe(2000);
    }

    /// <summary>Gets a valid image of every format and structure the sanitizer reads.</summary>
    public static TheoryData<string, byte[]> Samples => new()
    {
        { "jpeg with metadata", ImageSamples.JpegWithMetadata() },
        { "progressive jpeg", ImageSamples.JpegWithTwoScans() },
        { "png with metadata", ImageSamples.PngWithMetadata() },
        { "lossless webp", ImageSamples.Webp() },
        { "lossy webp", ImageSamples.LossyWebp(64, 48) },
        { "extended webp with metadata", ImageSamples.WebpWithMetadata(30, 20) },
    };
}
