using System.Text.RegularExpressions;

namespace SharedKernel.Repository.Tests.Conventions;

/// <summary>
/// Reads every <c>[LoggerMessage]</c> definition in the library sources with its package, event id, event name, level and message
/// placeholders. Event ids that are not literals are resolved through the <c>const int</c> members of the library sources, keyed by the
/// declaring file name, which is the type name because every file holds one top-level type.
/// </summary>
internal static partial class LogMessageScanner
{
    /// <summary>Finds the log message definitions under <paramref name="sourceRoot"/>, except build output.</summary>
    /// <param name="sourceRoot">The root of the library sources, <c>src/SharedKernel</c>.</param>
    /// <returns>Each definition; the event id or name is <see langword="null"/> when it is missing or cannot be resolved.</returns>
    internal static List<LogMessageDefinition> Definitions(string sourceRoot)
    {
        List<(string Package, string Type, string File, string Text)> files = [];
        var constants = new Dictionary<string, (string Type, string Value)>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            var segments = relative.Split(Path.DirectorySeparatorChar);
            if (segments.Contains("bin", StringComparer.Ordinal) || segments.Contains("obj", StringComparer.Ordinal))
            {
                continue;
            }

            var type = Path.GetFileName(file).Split('.')[0];
            var text = File.ReadAllText(file);
            files.Add((segments[0], type, relative.Replace(Path.DirectorySeparatorChar, '/'), text));
            foreach (Match match in IntegerDeclarationRegex().Matches(text))
            {
                constants[$"{type}.{match.Groups["member"].Value}"] = (type, match.Groups["value"].Value);
            }
        }

        List<LogMessageDefinition> definitions = [];
        foreach (var (package, type, file, text) in files)
        {
            foreach (Match match in LoggerMessageRegex().Matches(text))
            {
                definitions.Add(Parse(package, type, file, TelemetrySourceScanner.SplitArguments(text, match.Index + match.Length), constants));
            }
        }

        return definitions;
    }

    /// <summary>Reads the rows of a Markdown table whose first cell is a backticked package name and second cell an id or an id range.</summary>
    /// <param name="markdown">The Markdown text of <c>docs/telemetry.md</c>.</param>
    /// <returns>The event id blocks by package, and the events by package, id and name.</returns>
    internal static (Dictionary<string, (int Start, int End)> Blocks, HashSet<(string Package, int EventId, string EventName)> Events) DocumentedLogs(string markdown)
    {
        var blocks = new Dictionary<string, (int Start, int End)>(StringComparer.Ordinal);
        foreach (Match match in BlockRowRegex().Matches(markdown))
        {
            blocks[match.Groups["package"].Value] = (int.Parse(match.Groups["start"].Value, System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(match.Groups["end"].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        HashSet<(string Package, int EventId, string EventName)> events = [];
        foreach (Match match in EventRowRegex().Matches(markdown))
        {
            events.Add((match.Groups["package"].Value,
                int.Parse(match.Groups["id"].Value, System.Globalization.CultureInfo.InvariantCulture),
                match.Groups["name"].Value));
        }

        return (blocks, events);
    }

    private static LogMessageDefinition Parse(
        string package,
        string type,
        string file,
        List<string> arguments,
        Dictionary<string, (string Type, string Value)> constants)
    {
        string? eventId = null;
        string? eventName = null;
        string? message = null;
        var positional = arguments.Where(static argument => !NamedArgumentRegex().IsMatch(argument)).ToList();
        if (positional.Count > 0 && !positional[0].StartsWith("LogLevel.", StringComparison.Ordinal))
        {
            eventId = positional[0];
            message = positional.Count > 2 ? positional[2] : null;
        }
        else if (positional.Count > 1)
        {
            message = positional[1];
        }

        foreach (var argument in arguments)
        {
            var named = NamedArgumentRegex().Match(argument);
            if (!named.Success)
            {
                continue;
            }

            var value = named.Groups["value"].Value.Trim();
            switch (named.Groups["name"].Value)
            {
                case "EventId":
                    eventId = value;
                    break;
                case "EventName":
                    eventName = value.Trim('"');
                    break;
                case "Message":
                    message = value;
                    break;
            }
        }

        var placeholders = message is null
            ? []
            : PlaceholderRegex().Matches(message).Select(static match => match.Groups["name"].Value).ToList();
        return new LogMessageDefinition(package, file, eventId is null ? null : ResolveInteger(eventId, type, constants), eventName, placeholders);
    }

    private static int? ResolveInteger(string expression, string contextType, Dictionary<string, (string Type, string Value)> constants, int depth = 0)
    {
        var trimmed = expression.Trim();
        if (int.TryParse(trimmed, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        if (depth > 8)
        {
            return null;
        }

        var segments = trimmed.Split('.');
        var type = segments.Length > 1 ? segments[^2] : contextType;
        return constants.TryGetValue($"{type}.{segments[^1]}", out var declaration)
            ? ResolveInteger(declaration.Value, declaration.Type, constants, depth + 1)
            : null;
    }

    [GeneratedRegex("""\[LoggerMessage\(""")]
    private static partial Regex LoggerMessageRegex();

    [GeneratedRegex("""const\s+int\s+(?<member>\w+)\s*=\s*(?<value>\d+|[A-Za-z_][\w.]*)\s*;""")]
    private static partial Regex IntegerDeclarationRegex();

    [GeneratedRegex("""^(?<name>EventId|EventName|Level|Message|SkipEnabledCheck)\s*=\s*(?<value>.+)$""", RegexOptions.Singleline)]
    private static partial Regex NamedArgumentRegex();

    [GeneratedRegex("""(?<!\{)\{(?<name>[^{}:,]+)(?:[:,][^{}]*)?\}(?!\})""")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex("""^\|\s*`(?<package>SharedKernel[\w.]*)`\s*\|\s*(?<start>\d+)-(?<end>\d+)\s*\|""", RegexOptions.Multiline)]
    private static partial Regex BlockRowRegex();

    [GeneratedRegex("""^\|\s*`(?<package>SharedKernel[\w.]*)`\s*\|\s*(?<id>\d+)\s*\|\s*`(?<name>\w+)`\s*\|""", RegexOptions.Multiline)]
    private static partial Regex EventRowRegex();
}
