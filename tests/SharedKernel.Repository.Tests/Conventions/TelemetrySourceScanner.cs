using System.Text.RegularExpressions;

namespace PandaBear.Repository.Tests.Conventions;

/// <summary>
/// Reads the telemetry a library declares from its source: the meters and activity sources it creates, the instruments it creates
/// with their arguments, the attribute keys it sets and the durations it records. Names that are not literals are resolved through
/// the <c>const string</c> and <c>static string</c> members of the library sources, keyed by the declaring file name, which is the
/// type name because every file holds one top-level type.
/// </summary>
internal sealed partial class TelemetrySourceScanner
{
    private readonly List<(string Package, string Type, string File, string Text)> _files = [];
    private readonly Dictionary<string, (string Type, string Value)> _declarations = new(StringComparer.Ordinal);

    /// <summary>Reads every library source file under <paramref name="sourceRoot"/>, except build output and the testing packages.</summary>
    /// <param name="sourceRoot">The root of the library sources, <c>src/SharedKernel</c>.</param>
    internal TelemetrySourceScanner(string sourceRoot)
    {
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            var segments = relative.Split(Path.DirectorySeparatorChar);
            if (segments.Contains("bin", StringComparer.Ordinal)
                || segments.Contains("obj", StringComparer.Ordinal)
                || segments[0].StartsWith("SharedKernel.Testing", StringComparison.Ordinal))
            {
                continue;
            }

            var type = Path.GetFileName(file).Split('.')[0];
            var text = File.ReadAllText(file);
            _files.Add((segments[0], type, relative, text));
            foreach (Match match in DeclarationRegex().Matches(text))
            {
                _declarations[$"{type}.{match.Groups["member"].Value}"] = (type, match.Groups["value"].Value);
            }
        }
    }

    /// <summary>Gets the semantic-convention attribute keys a package may use without the <c>sharedkernel.</c> prefix.</summary>
    internal static IReadOnlySet<string> SemanticConventionAttributes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "error.type",
        "http.route",
        "http.request.method",
        "http.response.status_code",
        "db.system.name",
        "service.name",
        "service.version",
        "exception.type",
    };

    /// <summary>Finds every meter and activity source the libraries create, with the package that creates it.</summary>
    /// <returns>The creating file, its package and the resolved name, or <see langword="null"/> when the name could not be resolved.</returns>
    internal List<(string File, string Package, string? Name)> CreatedTelemetryNames()
    {
        List<(string File, string Package, string? Name)> names = [];
        foreach (var (package, type, file, text) in _files)
        {
            foreach (var match in TargetTypedCreationRegex().Matches(text).Concat(ExplicitCreationRegex().Matches(text)).Concat(FactoryCreationRegex().Matches(text)))
            {
                names.Add((file, package, Resolve(match.Groups["arg"].Value, type)));
            }
        }

        return names;
    }

    /// <summary>Finds every instrument the libraries create.</summary>
    /// <returns>The file, the instrument kind, the resolved name, unit and description, and the advice expression of each instrument.</returns>
    internal List<(string File, string Kind, string? Name, string? Unit, string? Description, string? Advice)> Instruments()
    {
        List<(string File, string Kind, string? Name, string? Unit, string? Description, string? Advice)> instruments = [];
        foreach (var (_, type, file, text) in _files)
        {
            foreach (Match match in InstrumentCreationRegex().Matches(text))
            {
                var arguments = NamedArguments(SplitArguments(text, match.Index + match.Length));
                instruments.Add((
                    file,
                    match.Groups["kind"].Value,
                    arguments.TryGetValue("name", out var name) ? Resolve(name, type) : null,
                    arguments.TryGetValue("unit", out var unit) ? Resolve(unit, type) : null,
                    arguments.TryGetValue("description", out var description) ? Resolve(description, type) : null,
                    arguments.GetValueOrDefault("advice")));
            }
        }

        return instruments;
    }

    /// <summary>Finds the attribute keys the libraries set on spans and measurements, and the values of the tag-name members of telemetry files.</summary>
    /// <returns>Each resolved key with the file that sets or declares it.</returns>
    internal List<(string File, string Key)> AttributeKeys()
    {
        List<(string File, string Key)> keys = [];
        foreach (var (_, type, file, text) in _files)
        {
            var usages = SetTagRegex().Matches(text)
                .Concat(KeyValuePairRegex().Matches(text))
                .Concat(TagListAddRegex().Matches(text));
            if (text.Contains("TagList", StringComparison.Ordinal))
            {
                usages = usages.Concat(InitializerItemRegex().Matches(text));
            }

            foreach (var match in usages)
            {
                if (Resolve(match.Groups["arg"].Value, type) is { } key)
                {
                    keys.Add((file, key));
                }
            }

            if (!type.EndsWith("Telemetry", StringComparison.Ordinal) && !TelemetryMarkerRegex().IsMatch(text))
            {
                continue;
            }

            foreach (Match match in DeclarationRegex().Matches(text))
            {
                if (TagMemberRegex().IsMatch(match.Groups["member"].Value) && Resolve(match.Groups["value"].Value, type) is { } key)
                {
                    keys.Add((file, key));
                }
            }
        }

        return keys;
    }

    /// <summary>Finds the files that record a measurement in milliseconds.</summary>
    /// <returns>The relative paths of the files whose <c>Record</c> call passes <c>TotalMilliseconds</c>.</returns>
    internal List<string> MillisecondRecordings() =>
        [.. _files.Where(static source => MillisecondRecordRegex().IsMatch(source.Text)).Select(static source => source.File)];

    private string? Resolve(string expression, string contextType, int depth = 0)
    {
        var trimmed = expression.Trim().Replace("global::", string.Empty, StringComparison.Ordinal);
        if (trimmed.StartsWith('"', StringComparison.Ordinal))
        {
            return trimmed.Trim('"');
        }

        if (depth > 8 || !IdentifierRegex().IsMatch(trimmed))
        {
            return null;
        }

        var segments = trimmed.Split('.');
        var type = segments.Length > 1 ? segments[^2] : contextType;
        return _declarations.TryGetValue($"{type}.{segments[^1]}", out var declaration)
            ? Resolve(declaration.Value, declaration.Type, depth + 1)
            : null;
    }

    private static List<string> SplitArguments(string text, int start)
    {
        List<string> arguments = [];
        var depth = 0;
        var inString = false;
        var current = start;
        for (var index = start; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"' && text[index - 1] != '\\')
            {
                inString = !inString;
            }
            else if (inString)
            {
                continue;
            }
            else if (character is '(' or '[' or '{' or '<')
            {
                depth++;
            }
            else if (character is ']' or '}' or '>')
            {
                depth--;
            }
            else if (character == ')' && depth == 0)
            {
                arguments.Add(text[current..index].Trim());
                break;
            }
            else if (character == ')')
            {
                depth--;
            }
            else if (character == ',' && depth == 0)
            {
                arguments.Add(text[current..index].Trim());
                current = index + 1;
            }
        }

        return arguments;
    }

    private static Dictionary<string, string> NamedArguments(List<string> arguments)
    {
        string[] positions = ["name", "unit", "description", "tags", "advice"];
        var named = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Count; index++)
        {
            var match = NamedArgumentRegex().Match(arguments[index]);
            if (match.Success)
            {
                named[match.Groups["name"].Value] = match.Groups["value"].Value;
            }
            else if (index < positions.Length)
            {
                named[positions[index]] = arguments[index];
            }
        }

        return named;
    }

    [GeneratedRegex("""(?:const\s+string|static\s+string)\s+(?<member>\w+)\s*(?:=>|=)\s*(?<value>"[^"]*"|[A-Za-z_][\w.]*)\s*;""")]
    private static partial Regex DeclarationRegex();

    [GeneratedRegex("""\b(?:Meter|ActivitySource)\s+\w+\s*(?:\{\s*get;\s*\}\s*)?=\s*new\s*\(\s*(?<arg>[^()]+?)\s*\)""")]
    private static partial Regex TargetTypedCreationRegex();

    [GeneratedRegex("""new\s+(?:Meter|ActivitySource)\s*\(\s*(?<arg>[^(),]+?)\s*[,)]""")]
    private static partial Regex ExplicitCreationRegex();

    [GeneratedRegex("""[Mm]eterFactory\.Create\(\s*(?<arg>[^()]+?)\s*\)""")]
    private static partial Regex FactoryCreationRegex();

    [GeneratedRegex("""\.Create(?<kind>Counter|Histogram|UpDownCounter|Gauge|ObservableCounter|ObservableGauge|ObservableUpDownCounter)(?:<\w+>)?\(""")]
    private static partial Regex InstrumentCreationRegex();

    [GeneratedRegex("""SetTag\(\s*(?<arg>"[^"]*"|[\w.:]+)\s*,""")]
    private static partial Regex SetTagRegex();

    [GeneratedRegex("""KeyValuePair<string,\s*object\?>\(\s*(?<arg>"[^"]*"|[\w.:]+)\s*,""")]
    private static partial Regex KeyValuePairRegex();

    [GeneratedRegex("""\btags\.Add\(\s*(?<arg>"[^"]*"|[\w.:]+)\s*,""")]
    private static partial Regex TagListAddRegex();

    [GeneratedRegex("""\{\s*(?<arg>"[^"]*"|[\w.:]+)\s*,""")]
    private static partial Regex InitializerItemRegex();

    [GeneratedRegex("""\b(?:ActivitySource|Meter|TagList|SetTag)\b|KeyValuePair<string,\s*object\?>""")]
    private static partial Regex TelemetryMarkerRegex();

    [GeneratedRegex("""^(?:Tag[A-Z]\w*|\w+Tag)$""")]
    private static partial Regex TagMemberRegex();

    [GeneratedRegex("""\.Record\([^;]*?TotalMilliseconds""")]
    private static partial Regex MillisecondRecordRegex();

    [GeneratedRegex("""^[A-Za-z_][\w]*(?:\.[A-Za-z_][\w]*)*$""")]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex("""^(?<name>\w+)\s*:(?!:)\s*(?<value>.+)$""", RegexOptions.Singleline)]
    private static partial Regex NamedArgumentRegex();
}
