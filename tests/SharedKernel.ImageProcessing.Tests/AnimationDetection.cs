namespace SharedKernel.ImageProcessing.Tests;

/// <summary>
/// Runs ImageAnimationDetector.Detect over byte arrays through either the span or the stream overload.
/// </summary>
internal static class AnimationDetection
{
    /// <summary>
    /// Detects the animation status of <paramref name="content"/> through the stream overload when
    /// <paramref name="viaStream"/> is set, otherwise through the span overload.
    /// </summary>
    public static ImageAnimationStatus Detect(byte[] content, bool viaStream)
    {
        if (!viaStream)
        {
            return ImageAnimationDetector.Detect(content);
        }

        using var stream = new MemoryStream(content);

        return ImageAnimationDetector.Detect(stream);
    }

    /// <summary>
    /// Detects every proper prefix of <paramref name="content"/> (lengths 0 to n-1) through both overloads and returns
    /// "overload:length" for each prefix that reports <paramref name="forbidden"/>.
    /// </summary>
    public static List<string> PrefixLengthsReporting(byte[] content, ImageAnimationStatus forbidden)
    {
        var offenders = new List<string>();
        for (var length = 0; length < content.Length; length++)
        {
            var prefix = content[..length];
            if (Detect(prefix, viaStream: false) == forbidden)
            {
                offenders.Add($"span:{length}");
            }

            if (Detect(prefix, viaStream: true) == forbidden)
            {
                offenders.Add($"stream:{length}");
            }
        }

        return offenders;
    }
}
