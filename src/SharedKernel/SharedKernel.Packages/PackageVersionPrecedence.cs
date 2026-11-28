using System.Text.RegularExpressions;

namespace SharedKernel.Packages;

/// <summary>Compares package versions when this library knows the package manager's precedence rules.</summary>
internal static partial class PackageVersionPrecedence
{
    private const string Number = "(0|[1-9][0-9]*)";
    private const string PrereleaseIdentifier = "(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)";
    private const string BuildIdentifier = "[0-9A-Za-z-]+";

    /// <summary>Compares the versions for a supported manager, or returns <see langword="null"/> when order is unknown.</summary>
    internal static int? Compare(PackageManagerKind? manager, PackageVersion left, PackageVersion right) =>
        manager is PackageManagerKind.Cargo or PackageManagerKind.Npm ? CompareSemVer(left, right) : null;

    /// <summary>Compares SemVer 2.0 precedence, or returns <see langword="null"/> when either version is invalid.</summary>
    internal static int? CompareSemVer(PackageVersion left, PackageVersion right)
    {
        var first = SemVerPattern().Match(left.Value);
        var second = SemVerPattern().Match(right.Value);
        if (!first.Success || !second.Success)
        {
            return null;
        }

        for (var part = 1; part <= 3; part++)
        {
            var comparison = CompareNumber(first.Groups[part].Value, second.Groups[part].Value);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return ComparePrerelease(first.Groups[4].Value, second.Groups[4].Value);
    }

    private static int ComparePrerelease(string first, string second)
    {
        if (first.Length == 0 || second.Length == 0)
        {
            return (first.Length == 0).CompareTo(second.Length == 0);
        }

        var firstParts = first.Split('.');
        var secondParts = second.Split('.');
        for (var part = 0; part < Math.Min(firstParts.Length, secondParts.Length); part++)
        {
            var left = firstParts[part];
            var right = secondParts[part];
            var leftNumeric = left.All(char.IsAsciiDigit);
            var rightNumeric = right.All(char.IsAsciiDigit);
            var comparison = (leftNumeric, rightNumeric) switch
            {
                (true, true) => CompareNumber(left, right),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(left, right),
            };
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return firstParts.Length.CompareTo(secondParts.Length);
    }

    private static int CompareNumber(string first, string second)
    {
        // Canonical numeric identifiers have no leading zeros. Compare length first to avoid integer overflow.
        var comparison = first.Length.CompareTo(second.Length);
        return comparison != 0 ? comparison : string.CompareOrdinal(first, second);
    }

    [GeneratedRegex(@"\A" + Number + @"\." + Number + @"\." + Number +
        "(?:-(" + PrereleaseIdentifier + @"(?:\." + PrereleaseIdentifier + ")*))?" +
        @"(?:\+" + BuildIdentifier + @"(?:\." + BuildIdentifier + @")*)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex SemVerPattern();
}
