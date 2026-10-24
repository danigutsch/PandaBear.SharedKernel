using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace PandaBear.CI.Tool;

/// <summary>Finds and reads Cobertura coverage reports, merging the lines of every report that names a source file.</summary>
internal static partial class CoberturaReport
{
    /// <summary>The file name pattern of the Cobertura reports that <c>run --coverage</c> writes and a report directory is searched for.</summary>
    public const string FilePattern = "*.cobertura*.xml";

    /// <summary>
    /// Returns the report files that <paramref name="reports"/> name: each file itself, and the <see cref="FilePattern"/>
    /// files of each directory, searched recursively.
    /// </summary>
    /// <exception cref="InvalidDataException">A path does not exist, or the paths name no report.</exception>
    public static string[] Find(string root, IEnumerable<string> reports)
    {
        var files = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var report in reports)
        {
            var path = Path.GetFullPath(report, root);
            if (Directory.Exists(path))
            {
                files.UnionWith(Directory.EnumerateFiles(path, FilePattern, SearchOption.AllDirectories));
            }
            else if (File.Exists(path))
            {
                files.Add(path);
            }
            else
            {
                throw new InvalidDataException("The coverage report does not exist: " + report + ".");
            }
        }

        return files.Count == 0 ? throw new InvalidDataException("No Cobertura report was found in " + string.Join(", ", reports) + ".") : [.. files];
    }

    /// <summary>Reads the line coverage of each source file that <paramref name="isIncluded"/> accepts, by repository-relative path.</summary>
    /// <remarks>
    /// A document type declaration is refused, so a report cannot expand entities or reach another file. A line is hit
    /// when any report hits it, and its conditions are those of the report that covers the most.
    /// </remarks>
    /// <exception cref="InvalidDataException">A report is not Cobertura or has an invalid line.</exception>
    /// <exception cref="XmlException">A report is not well-formed XML or declares a document type.</exception>
    public static Dictionary<string, Dictionary<int, LineCoverage>> Read(string root, IEnumerable<string> files, Func<string, bool> isIncluded)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        var result = new Dictionary<string, Dictionary<int, LineCoverage>>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            XDocument document;
            using (var reader = XmlReader.Create(file, settings))
            {
                document = XDocument.Load(reader);
            }

            if (document.Root?.Name != "coverage")
            {
                throw new InvalidDataException(file + " is not a Cobertura report: its root element is not coverage.");
            }

            var sources = document.Descendants("source").Select(static element => element.Value).ToArray();
            foreach (var entry in document.Descendants("class"))
            {
                var filename = entry.Attribute("filename")?.Value ?? throw new InvalidDataException(file + " has a class without a filename.");
                var path = Relative(filename, root);
                if (path is null || !File.Exists(Path.Combine(root, path)))
                {
                    path = sources.Select(source => Relative(Path.Combine(source, filename), root))
                        .FirstOrDefault(candidate => candidate is not null && File.Exists(Path.Combine(root, candidate)));
                }

                if (path is null || !isIncluded(path))
                {
                    continue;
                }

                if (!result.TryGetValue(path, out var lines))
                {
                    lines = [];
                    result.Add(path, lines);
                }

                foreach (var line in entry.Elements("lines").Elements("line"))
                {
                    if (!int.TryParse(line.Attribute("number")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                        || !long.TryParse(line.Attribute("hits")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var hits)
                        || number == 0)
                    {
                        throw new InvalidDataException(file + " has a line without a positive number or a nonnegative hit count.");
                    }

                    var branches = 0;
                    var coveredBranches = 0;
                    if (line.Attribute("branch")?.Value == "true")
                    {
                        var match = Condition().Match(line.Attribute("condition-coverage")?.Value ?? string.Empty);
                        if (!match.Success
                            || !int.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out coveredBranches)
                            || !int.TryParse(match.Groups[2].Value, CultureInfo.InvariantCulture, out branches)
                            || coveredBranches > branches)
                        {
                            throw new InvalidDataException(file + " has malformed condition coverage on line " + number.ToString(CultureInfo.InvariantCulture) + ".");
                        }
                    }

                    lines.TryGetValue(number, out var previous);
                    lines[number] = new LineCoverage(previous.Hit || hits > 0, Math.Max(previous.CoveredBranches, coveredBranches), Math.Max(previous.Branches, branches));
                }
            }
        }

        return result;
    }

    /// <summary>Returns <paramref name="filename"/> relative to <paramref name="root"/> with <c>/</c> separators, or <see langword="null"/> outside it.</summary>
    public static string? Relative(string filename, string root)
    {
        var relative = Path.GetRelativePath(root, Path.GetFullPath(filename.Replace('\\', '/'), root)).Replace('\\', '/');
        return relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? null : relative;
    }

    [GeneratedRegex(@"\A[0-9]+(?:\.[0-9]+)?% \(([0-9]+)/([0-9]+)\)\z", RegexOptions.CultureInvariant)]
    private static partial Regex Condition();
}
