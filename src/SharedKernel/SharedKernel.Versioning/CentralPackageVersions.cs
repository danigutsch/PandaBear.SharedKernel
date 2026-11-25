using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SharedKernel.Versioning;

/// <summary>Reads the central package versions of <c>Directory.Packages.props</c> that a project of one target framework uses.</summary>
internal static partial class CentralPackageVersions
{
    /// <summary>
    /// Returns the version of each package that a <c>PackageVersion</c> item includes, after the <c>Update</c> items whose
    /// item group or item is unconditional or has the condition <c>'$(TargetFramework)' == '<paramref name="framework" />'</c>.
    /// </summary>
    /// <param name="document">The <c>Directory.Packages.props</c> document.</param>
    /// <param name="framework">The target framework of the consuming project, such as <c>net11.0</c>.</param>
    /// <returns>The package versions by package ID.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document" /> or <paramref name="framework" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="framework" /> is empty or white space.</exception>
    /// <exception cref="InvalidOperationException">
    /// An item or item group has another condition, an item has no version, or an <c>Update</c> names a package that no item includes.
    /// </exception>
    public static Dictionary<string, string> Read(XDocument document, string framework)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(framework);

        Dictionary<string, string> versions = new(StringComparer.Ordinal);
        List<XElement> updates = [];
        foreach (var item in document.Descendants("PackageVersion"))
        {
            if (!Applies(item, framework) || !Applies(item.Parent, framework))
            {
                continue;
            }

            var version = (string?)item.Attribute("Version")
                ?? throw new InvalidOperationException("Directory.Packages.props has a PackageVersion without a Version.");
            if ((string?)item.Attribute("Include") is { } include)
            {
                versions[include] = version;
            }
            else
            {
                updates.Add(item);
            }
        }

        foreach (var update in updates)
        {
            var id = (string?)update.Attribute("Update")
                ?? throw new InvalidOperationException("Directory.Packages.props has a PackageVersion without Include or Update.");
            if (!versions.ContainsKey(id))
            {
                throw new InvalidOperationException("Directory.Packages.props updates " + id + ", which no PackageVersion includes.");
            }

            versions[id] = (string)update.Attribute("Version")!;
        }

        return versions;
    }

    private static bool Applies(XElement? element, string framework)
    {
        if ((string?)element?.Attribute("Condition") is not { } condition || condition.Trim().Length == 0)
        {
            return true;
        }

        var match = FrameworkCondition().Match(condition);
        if (!match.Success)
        {
            throw new InvalidOperationException(
                "Directory.Packages.props has a PackageVersion condition that is not '$(TargetFramework)' == '<framework>': " + condition.Trim());
        }

        return string.Equals(match.Groups["framework"].Value, framework, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^\s*'\$\(TargetFramework\)'\s*==\s*'(?<framework>[^']+)'\s*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FrameworkCondition();
}
