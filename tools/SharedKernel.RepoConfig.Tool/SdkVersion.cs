using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PandaBear.RepoConfig.Tool;

/// <summary>A .NET SDK version such as <c>10.0.100</c> or <c>10.0.100-rc.1.25451.107</c>. Only well-formed versions exist.</summary>
internal sealed partial record SdkVersion
{
    private SdkVersion(string value, int major)
    {
        Value = value;
        Major = major;
    }

    /// <summary>Gets the version text.</summary>
    public string Value
    {
        get;
    }

    /// <summary>Gets the major version, which selects the target framework.</summary>
    public int Major
    {
        get;
    }

    /// <summary>Parses <paramref name="text"/> as <c>major.minor.patch</c> with an optional dot-separated pre-release part.</summary>
    /// <returns><see langword="false"/> when the text is not such a version or its major version is below 6.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out SdkVersion? version)
    {
        version = null;
        if (text is null || !Pattern().IsMatch(text)
            || !int.TryParse(text.AsSpan(0, text.IndexOf('.', StringComparison.Ordinal)), NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || major < 6)
        {
            return false;
        }

        version = new SdkVersion(text, major);
        return true;
    }

    [GeneratedRegex(@"^\d{1,2}\.\d{1,3}\.\d{1,4}(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Pattern();
}
