using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SharedKernel.DevContainers;

/// <summary>Creates a Dev Container configuration for a caller-selected image pinned by a SHA-256 digest.</summary>
public static partial class DevcontainerImageConfiguration
{
    private const int MaxImageNameLength = 255;
    private const int MaxImageReferenceLength = MaxImageNameLength + 201; // Optional tag and required SHA-256 suffix.
    private const string HostLabel = "[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?";
    private const string Host = HostLabel + @"(?:\." + HostLabel + @")*|\[[A-Fa-f0-9:]+\]";
    private const string Registry = "(?:(?:" + Host + ")(?::[0-9]+)?/)?";
    private const string PathSegment = "[a-z0-9]+(?:(?:[._]|__|-+)[a-z0-9]+)*";
    private const string ImageName = Registry + PathSegment + "(?:/" + PathSegment + ")*";
    private const string Tag = "(?::[A-Za-z0-9_][A-Za-z0-9_.-]{0,127})?";

    /// <summary>Renders the image configuration when <paramref name="image"/> has a canonical SHA-256 pin.</summary>
    /// <param name="name">The name displayed by the Dev Container client.</param>
    /// <param name="image">The caller's image reference.</param>
    /// <param name="configuration">The resulting JSON or <see langword="null"/> when the image is invalid.</param>
    /// <returns>Whether the image reference is valid and digest-pinned.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public static bool TryCreate(string name, string? image, [NotNullWhen(true)] out string? configuration)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (string.IsNullOrEmpty(image) || image.Length > MaxImageReferenceLength ||
            PinnedImage().Match(image) is not { Success: true } match || match.Groups["name"].Length > MaxImageNameLength)
        {
            configuration = null;
            return false;
        }

        configuration = $$"""
            {
              "name": "{{JsonEncodedText.Encode(name)}}",
              "image": "{{JsonEncodedText.Encode(image)}}"
            }
            """ + "\n";
        return true;
    }

    [GeneratedRegex(@"\A(?<name>" + ImageName + ")" + Tag + @"@sha256:[a-f0-9]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex PinnedImage();
}
