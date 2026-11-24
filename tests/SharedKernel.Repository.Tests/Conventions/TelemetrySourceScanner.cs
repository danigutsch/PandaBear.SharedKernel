using System.Text.RegularExpressions;

namespace PandaBear.Repository.Tests.Conventions;

/// <summary>
/// Reads the telemetry a library declares from its source: the meters and activity sources it creates, the instruments it creates
/// with their arguments, the attribute keys it sets and the durations it records. Names that are not literals are resolved through
/// the <c>const string</c>, <c>static string</c> and <c>static readonly string</c> members of the library sources, keyed by the declaring file name, which is the
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
    /// <returns>Each key expression with the file that sets or declares it and its resolved value, or <see langword="null"/> when it is not a constant.</returns>
    internal List<(string File, string Expression, string? Key)> AttributeKeys()
    {
        List<(string File, string Expression, string? Key)> keys = [];
        foreach (var (_, type, file, text) in _files)
        {
            var usages = SetTagRegex().Matches(text)
                .Concat(KeyValuePairRegex().Matches(text))
                .Concat(TagListVariableRegex().Matches(text)
                    .Select(static variable => variable.Groups["name"].Value)
                    .Distinct(StringComparer.Ordinal)
                    .SelectMany(name => Regex.Matches(text, $"""\b{Regex.Escape(name)}\.Add\(\s*(?<arg>"[^"]*"|[\w.:]+)\s*,""")));
            if (text.Contains("TagList", StringComparison.Ordinal))
            {
                usages = usages.Concat(InitializerItemRegex().Matches(text));
            }

            foreach (var match in usages)
            {
                var expression = match.Groups["arg"].Value;
                keys.Add((file, expression, Resolve(expression, type)));
            }

            if (!type.EndsWith("Telemetry", StringComparison.Ordinal) && !TelemetryMarkerRegex().IsMatch(text))
            {
                continue;
            }

            foreach (Match match in DeclarationRegex().Matches(text))
            {
                if (TagMemberRegex().IsMatch(match.Groups["member"].Value))
                {
                    keys.Add((file, match.Groups["member"].Value, Resolve(match.Groups["value"].Value, type)));
                }
            }
        }

        return keys;
    }

    /// <summary>Finds the files that create or record into a histogram and also use a milliseconds value.</summary>
    /// <returns>The relative paths of the files that use <c>Milliseconds</c>, <c>TotalMilliseconds</c> or <c>ElapsedMilliseconds</c> next to a histogram.</returns>
    internal List<string> MillisecondRecordings() =>
        [.. _files.Where(static source => HistogramUseRegex().IsMatch(source.Text) && MillisecondsRegex().IsMatch(source.Text)).Select(static source => source.File)];

    /// <summary>Finds the public string constants and properties that name a meter or activity source a package creates.</summary>
    /// <returns>The packages that create a meter or activity source, each with the public members, in public types, whose value is its name.</returns>
    internal Dictionary<string, List<string>> PublicNameMembers()
    {
        var packages = CreatedTelemetryNames().Select(static entry => entry.Package).ToHashSet(StringComparer.Ordinal);
        var members = packages.ToDictionary(static package => package, static _ => new List<string>(), StringComparer.Ordinal);
        foreach (var (_, type, _, text) in _files)
        {
            if (!Regex.IsMatch(text, $"""\bpublic\s+(?:static\s+|sealed\s+|abstract\s+|partial\s+)*class\s+{Regex.Escape(type)}\b"""))
            {
                continue;
            }

            foreach (Match match in PublicDeclarationRegex().Matches(text))
            {
                if (Resolve(match.Groups["value"].Value, type) is { } value && members.TryGetValue(value, out var found))
                {
                    found.Add($"{type}.{match.Groups["member"].Value}");
                }
            }
        }

        return members;
    }

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

    /// <summary>Splits the arguments of the call whose opening parenthesis ends just before <paramref name="start"/>, respecting nesting and strings.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="start">The index just after the opening parenthesis.</param>
    /// <returns>The trimmed top-level arguments.</returns>
    internal static List<string> SplitArguments(string text, int start)
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

    [GeneratedRegex("""(?:const\s+string|static\s+readonly\s+string|static\s+string)\s+(?<member>\w+)\s*(?:=>|=)\s*(?<value>"[^"]*"|[A-Za-z_][\w.]*)\s*;""")]
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

    [GeneratedRegex("""\bpublic\s+(?:const\s+string|static\s+readonly\s+string|static\s+string)\s+(?<member>\w+)\s*(?:=>|=)\s*(?<value>"[^"]*"|[A-Za-z_][\w.]*)\s*;""")]
    private static partial Regex PublicDeclarationRegex();

    [GeneratedRegex("""\bTagList\s+(?<name>\w+)\s*(?:=|;)|\bvar\s+(?<name>\w+)\s*=\s*new\s+TagList\b""")]
    private static partial Regex TagListVariableRegex();

    [GeneratedRegex("""\{\s*(?<arg>"[^"]*"|[\w.:]+)\s*,""")]
    private static partial Regex InitializerItemRegex();

    [GeneratedRegex("""\b(?:ActivitySource|Meter|TagList|SetTag)\b|KeyValuePair<string,\s*object\?>""")]
    private static partial Regex TelemetryMarkerRegex();

    [GeneratedRegex("""^(?:Tag[A-Z]\w*|\w+Tag)$""")]
    private static partial Regex TagMemberRegex();

    [GeneratedRegex("""CreateHistogram|Histogram<|\.Record\(""")]
    private static partial Regex HistogramUseRegex();

    [GeneratedRegex("""\b(?:Total|Elapsed)?Milliseconds\b""")]
    private static partial Regex MillisecondsRegex();

    [GeneratedRegex("""^[A-Za-z_][\w]*(?:\.[A-Za-z_][\w]*)*$""")]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex("""^(?<name>\w+)\s*:(?!:)\s*(?<value>.+)$""", RegexOptions.Singleline)]
    private static partial Regex NamedArgumentRegex();
}
