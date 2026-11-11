using PandaBear.Testing;

namespace PandaBear.ImageSanitizing.Tests;

/// <summary>
/// Tests that the sanitizer reports whether an image is animated (APNG, animated WebP, AVIF image sequences), and refuses it only when
/// <see cref="ImageSanitizerOptions.RejectAnimation"/> is set.
/// </summary>
[TestScope(TestScope.Unit)]
public sealed class ImageSanitizerAnimationTests
{
    private static readonly ImageSanitizerOptions StillOnly = ImageSanitizerOptions.Default with
    {
        RejectAnimation = true,
        AllowAvif = true
    };

    private static readonly ImageSanitizerOptions AnyAvif = ImageSanitizerOptions.Default with
    {
        AllowAvif = true
    };

    [Theory]
    [MemberData(nameof(Animated))]
    public void An_animated_image_is_refused_when_animation_is_rejected(string because, byte[] file)
    {
        // Act
        var result = ImageSanitizer.Sanitize(file, StillOnly);

        // Assert
        result.ShouldBeFailure(Results.ResultStatus.Invalid, Results.ResultErrorCodes.Invalid, because)
            .ShouldHaveValidationError("file", ImageSanitizer.AnimatedMessage);
    }

    [Theory]
    [MemberData(nameof(Animated))]
    public void An_animated_image_is_accepted_and_reported_as_animated_by_default(string because, byte[] file)
    {
        // Act
        var image = ImageSanitizer.Sanitize(file, AnyAvif).ShouldBeSuccess(because);

        // Assert
        image.IsAnimated.ShouldBeTrue(because);
    }

    [Theory]
    [MemberData(nameof(Still))]
    public void A_still_image_is_accepted_when_animation_is_rejected(string because, byte[] file)
    {
        // Act
        var image = ImageSanitizer.Sanitize(file, StillOnly).ShouldBeSuccess(because);

        // Assert
        image.IsAnimated.ShouldBeFalse(because);
    }

    [Fact]
    public void Animation_is_accepted_by_default()
    {
        // Act
        var options = ImageSanitizerOptions.Default;

        // Assert
        options.RejectAnimation.ShouldBeFalse();
    }

    /// <summary>Gets an animated image of every format that can be animated.</summary>
    public static TheoryData<string, byte[]> Animated => new()
    {
        { "an APNG", ImageSamples.AnimatedPng() },
        { "a WebP with an ANIM chunk", ImageSamples.ExtendedWebp(animationFlag: false, "ANIM") },
        { "a WebP with an ANMF chunk", ImageSamples.ExtendedWebp(animationFlag: false, "ANMF") },
        { "a WebP whose extended header sets the animation flag", ImageSamples.ExtendedWebp(animationFlag: true, extraChunk: null) },
        { "an AVIF image sequence", AvifSamples.AnimatedAvif() },
        { "an AVIF with a movie box", AvifSamples.AvifWithMovie() },
    };

    /// <summary>Gets a still image of every format.</summary>
    public static TheoryData<string, byte[]> Still => new()
    {
        { "a JPEG", ImageSamples.Jpeg() },
        { "a PNG", ImageSamples.PngWithMetadata() },
        { "a WebP", ImageSamples.Webp() },
        { "an extended WebP", ImageSamples.ExtendedWebp(animationFlag: false, extraChunk: null) },
        { "an AVIF", AvifSamples.Avif() },
    };
}
