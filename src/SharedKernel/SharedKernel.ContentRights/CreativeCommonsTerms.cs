using System.Diagnostics.CodeAnalysis;
using SharedKernel.BuildingBlocks;

namespace SharedKernel.ContentRights;

/// <summary>Identifies supported Creative Commons terms from an explicit declaration and its official URL.</summary>
internal static class CreativeCommonsTerms
{
    private const string OfficialHost = "creativecommons.org";
    private const string CanonicalBase = "https://" + OfficialHost + "/";

    // An empty port denotes a generic or international tool. Translations share the port of the legal code.
    // Source: creativecommons/cc-legal-tools-data, snapshot 91780e140892982eb3e0d530591ccbbca0d4690d, legacy/legalcode.
    private static readonly Dictionary<string, string[]> LicensePortsByVersion = new(StringComparer.Ordinal)
    {
        ["4.0"] = [""],
        ["3.0"] =
        [
            "", "am", "at", "au", "az", "br", "ca", "ch", "cl", "cn", "cr", "cz", "de", "ec", "ee", "eg", "es", "fr",
            "ge", "gr", "gt", "hk", "hr", "ie", "igo", "it", "lu", "nl", "no", "nz", "ph", "pl", "pr", "pt", "ro", "rs",
            "sg", "th", "tw", "ug", "us", "ve", "vn", "za",
        ],
        ["2.5"] =
        [
            "", "ar", "au", "bg", "br", "ca", "ch", "cn", "co", "dk", "es", "hr", "hu", "il", "in", "it", "mk", "mt",
            "mx", "my", "nl", "pe", "pl", "pt", "scotland", "se", "si", "tw", "za",
        ],
        ["2.1"] = ["au", "ca", "es", "jp"],
        ["2.0"] = ["", "at", "au", "be", "br", "ca", "cl", "de", "es", "fr", "hr", "it", "jp", "kr", "nl", "pl", "tw", "uk", "za"],
        ["1.0"] = ["", "fi", "il", "nl"],
    };

    /// <summary>Returns whether the declared kind identifies a supported Creative Commons family or tool.</summary>
    /// <param name="kind">The entered kind.</param>
    /// <returns>Whether the corresponding terms come from Creative Commons.</returns>
    public static bool IsStandardTool(ContentLicenseKind kind) =>
        kind is >= ContentLicenseKind.CcBy and <= ContentLicenseKind.PublicDomainMark;

    /// <summary>Identifies the standard terms addressed by a known image license code.</summary>
    /// <param name="code">The recorded image license code.</param>
    /// <param name="kind">The supported family or tool.</param>
    /// <param name="address">The official terms address for the code.</param>
    /// <param name="version">The version identified by the code.</param>
    /// <returns>Whether the code identifies a supported standard tool.</returns>
    public static bool TryIdentifyImageCode(
        string? code, out ContentLicenseKind kind, [NotNullWhen(true)] out WebUrl? address, [NotNullWhen(true)] out string? version)
    {
        if (ImageLicense.KnownUrl(code) is { } knownAddress)
        {
            for (var candidate = ContentLicenseKind.CcBy; IsStandardTool(candidate); candidate++)
            {
                if (TryIdentify(candidate, knownAddress, out _, out version, out _))
                {
                    kind = candidate;
                    address = knownAddress;
                    return true;
                }
            }
        }

        kind = ContentLicenseKind.Unknown;
        address = null;
        version = null;
        return false;
    }

    /// <summary>Resolves the exact tool, version and jurisdiction addressed by the supplied official URL.</summary>
    /// <param name="kind">The explicitly declared family or tool.</param>
    /// <param name="terms">The validated address supplied with the declaration.</param>
    /// <param name="canonical">The HTTPS deed address, when recognized.</param>
    /// <param name="version">The version addressed by the terms, when recognized.</param>
    /// <param name="jurisdiction">The jurisdiction of a port, when supplied.</param>
    /// <returns>Whether the address identifies the declared standard tool.</returns>
    public static bool TryIdentify(
        ContentLicenseKind kind, WebUrl terms, [NotNullWhen(true)] out WebUrl? canonical, [NotNullWhen(true)] out string? version, out string? jurisdiction)
    {
        canonical = null;
        version = null;
        jurisdiction = null;

        // WebUrl validates the authority and retains the supplied path. Do not use Uri.AbsolutePath, which resolves dot segments.
        var address = terms.Value;
        var hostStart = address.IndexOf("://", StringComparison.Ordinal) + 3;
        var pathStart = address.IndexOf('/', hostStart, StringComparison.Ordinal);
        if (pathStart < 0 || !address.AsSpan(hostStart, pathStart - hostStart).SequenceEqual(OfficialHost))
        {
            return false;
        }

        var path = address[(pathStart + 1)..];
        var suffix = path.AsSpan().IndexOfAny('?', '#');
        if (suffix >= 0)
        {
            path = path[..suffix];
        }

        if (path.EndsWith('/', StringComparison.Ordinal))
        {
            path = path[..^1];
        }

        var segments = path.Split('/');
        if (segments.Length is < 3 or > 5 || segments.Contains(string.Empty, StringComparer.Ordinal))
        {
            return false;
        }

        var isPublicDomainTool = kind is ContentLicenseKind.Cc0 or ContentLicenseKind.PublicDomainMark;
        var toolSegment = isPublicDomainTool ? "publicdomain" : "licenses";
        var statedVersion = segments[2];
        var tool = ToolName(kind, statedVersion);
        if (segments[0] != toolSegment || segments[1] != tool)
        {
            return false;
        }

        var hasPort = segments.Length >= 4 && !IsPresentationSegment(segments[3], kind);
        var statedJurisdiction = hasPort ? segments[3] : null;
        var toolSegments = hasPort ? 4 : 3;
        if (segments.Length > toolSegments
            && (segments.Length != toolSegments + 1 || !IsPresentationSegment(segments[toolSegments], kind)))
        {
            return false;
        }

        var recognizedVersion = isPublicDomainTool
            ? statedVersion == "1.0" && statedJurisdiction is null
            : LicensePortsByVersion.TryGetValue(statedVersion, out var ports)
                && ports.Contains(statedJurisdiction ?? string.Empty, StringComparer.Ordinal);
        if (!recognizedVersion)
        {
            return false;
        }

        var portPath = statedJurisdiction is null ? string.Empty : statedJurisdiction + "/";
        canonical = WebUrl.CreateHttps($"{CanonicalBase}{toolSegment}/{tool}/{statedVersion}/{portPath}").Value;
        version = statedVersion;
        jurisdiction = statedJurisdiction;
        return true;
    }

    private static bool IsPresentationSegment(string segment, ContentLicenseKind kind)
    {
        var delimiter = segment.IndexOf('.', StringComparison.Ordinal);
        var name = delimiter < 0 ? segment : segment[..delimiter];
        if (name != "deed" && (name != "legalcode" || kind == ContentLicenseKind.PublicDomainMark))
        {
            return false;
        }

        if (delimiter < 0)
        {
            return true;
        }

        var language = segment[(delimiter + 1)..].Split('-');
        return language[0].Length is >= 2 and <= 3
            && language[0].All(char.IsAsciiLetterLower)
            && language.Skip(1).All(part => part.Length is >= 2 and <= 8 && part.All(char.IsAsciiLetterOrDigit));
    }

    private static string ToolName(ContentLicenseKind kind, string version) => kind switch
    {
        ContentLicenseKind.CcBy => "by",
        ContentLicenseKind.CcBySa => "by-sa",
        ContentLicenseKind.CcByNd => "by-nd",
        ContentLicenseKind.CcByNc => "by-nc",
        ContentLicenseKind.CcByNcSa => "by-nc-sa",
        ContentLicenseKind.CcByNcNd when version == "1.0" => "by-nd-nc",
        ContentLicenseKind.CcByNcNd => "by-nc-nd",
        ContentLicenseKind.Cc0 => "zero",
        ContentLicenseKind.PublicDomainMark => "mark",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
