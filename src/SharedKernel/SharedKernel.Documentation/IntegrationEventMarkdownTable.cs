using System.Text;
using System.Text.RegularExpressions;

namespace SharedKernel.Documentation;

/// <summary>
/// Builds Markdown integration-event inventories from source-backed contracts and handlers.
/// </summary>
internal static partial class IntegrationEventMarkdownTable
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    /// <summary>
    /// Builds an integration-event inventory table from C# source files.
    /// </summary>
    public static string Build(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var files = SourceFiles(sourcePath)
            .Select(file => (File: file, DisplayPath: DisplayPath(sourcePath, file), Content: File.ReadAllText(file.FullName, Utf8NoBom)))
            .ToArray();
        var rows = EventContracts(files)
            .OrderBy(row => row.EventName, StringComparer.Ordinal)
            .Select(row => $"| {row.EventName} | `{row.EventType}` | {row.EventVersion} | {Sources(files, row.EventName)} | {Consumers(files, row.EventName)} | {Handlers(files, row.EventName)} |")
            .ToArray();

        return string.Join(
            '\n',
            [
                "| Event | Event type | Version | Producers | Consumers | Handlers |",
                "| --- | --- | --- | --- | --- | --- |",
                .. (rows.Length == 0 ? ["| n/a | n/a | n/a | No integration-event contracts discovered. | n/a | n/a |"] : rows),
            ]);
    }

    private static IEnumerable<(string EventName, string EventType, string EventVersion)> EventContracts(IReadOnlyCollection<(FileInfo File, string DisplayPath, string Content)> files)
    {
        foreach (var content in files.Select(static item => item.Content))
        {
            var eventMatch = IntegrationEventRegex().Match(content);
            if (!eventMatch.Success)
            {
                continue;
            }

            var eventType = EventTypeRegex().Match(content);
            var eventVersion = EventVersionRegex().Match(content);
            yield return (
                eventMatch.Groups[1].Value,
                eventType.Success ? eventType.Groups[1].Value : "not declared",
                eventVersion.Success ? eventVersion.Groups[1].Value : "not declared");
        }
    }

    private static IEnumerable<FileInfo> SourceFiles(string sourcePath) =>
        new DirectoryInfo(sourcePath)
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(static file => !file.FullName.Split(Path.DirectorySeparatorChar).Contains("bin", StringComparer.Ordinal))
            .Where(static file => !file.FullName.Split(Path.DirectorySeparatorChar).Contains("obj", StringComparer.Ordinal))
            .OrderBy(file => file.FullName, StringComparer.Ordinal);

    private static string Sources(IReadOnlyCollection<(FileInfo File, string DisplayPath, string Content)> files, string eventName)
    {
        var producers = FilesContaining(files, $"new {eventName}(")
            .ToArray();

        return FormatList(producers, "not discovered from source");
    }

    private static string Consumers(IReadOnlyCollection<(FileInfo File, string DisplayPath, string Content)> files, string eventName)
    {
        var consumerRegex = ConsumerRegex(eventName);
        var consumers = files
            .Where(file => consumerRegex.IsMatch(file.Content))
            .Select(file => file.DisplayPath);

        return FormatList(consumers, "not registered");
    }

    private static string Handlers(IReadOnlyCollection<(FileInfo File, string DisplayPath, string Content)> files, string eventName)
    {
        var handlerRegex = HandlerRegex(eventName);
        var handlers = files
            .Select(file => handlerRegex.Match(file.Content))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(handler => handler, StringComparer.Ordinal)
            .ToArray();

        return handlers.Length == 0 ? "not discovered" : string.Join("<br>", handlers.Select(static handler => $"`{handler}`"));
    }

    private static IEnumerable<string> FilesContaining(IReadOnlyCollection<(FileInfo File, string DisplayPath, string Content)> files, string marker) =>
        files
            .Where(file => file.Content.Contains(marker, StringComparison.Ordinal))
            .Select(file => file.DisplayPath)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

    private static string DisplayPath(string sourcePath, FileInfo file)
    {
        return Path.GetRelativePath(sourcePath, file.FullName)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string FormatList(IEnumerable<string> values, string emptyValue)
    {
        var items = values.Select(static value => $"`{value}`").ToArray();
        return items.Length == 0 ? emptyValue : string.Join("<br>", items);
    }

    [GeneratedRegex(@"public\s+sealed\s+record\s+(\w+)\s*\(.*?\)\s*:\s*IIntegrationEvent", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex IntegrationEventRegex();

    [GeneratedRegex(@"public\s+static\s+string\s+EventType\s*=>\s*""([^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex EventTypeRegex();

    [GeneratedRegex(@"public\s+static\s+int\s+EventVersion\s*=>\s*(\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex EventVersionRegex();

    private static Regex HandlerRegex(string eventName) => new(
        $@"(?:class|record)\s+(\w+).*?:\s*IIntegrationEventHandler<{Regex.Escape(eventName)}>",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static Regex ConsumerRegex(string eventName) => new(
        $@"AddIntegrationEventConsumer(?:<\s*{Regex.Escape(eventName)}\s*>)?\(\s*{Regex.Escape(eventName)}\.EventType",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));
}
