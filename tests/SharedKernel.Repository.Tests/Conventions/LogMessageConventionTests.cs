using System.Text.RegularExpressions;

namespace PandaBear.Repository.Tests.Conventions;

/// <summary>
/// Guards the log standard in <c>docs/telemetry.md</c>: every <c>[LoggerMessage]</c> has an explicit event id inside its package's
/// block and a PascalCase event name unique in its package, event ids are unique across SharedKernel because hosts log several packages
/// into one process, placeholders are PascalCase, and the documentation lists every event.
/// </summary>
public sealed partial class LogMessageConventionTests
{
    private static string RepositoryRoot => PackagingConventionTestFiles.GetRepositoryRoot();

    private static string SourceRoot => Path.Combine(RepositoryRoot, "src", "SharedKernel");

    private static string TelemetryDocument => File.ReadAllText(Path.Combine(RepositoryRoot, "docs", "telemetry.md"));

    [Fact]
    public void Every_log_message_has_an_event_ID_and_a_pascal_case_event_name_unique_in_its_package()
    {
        // Act
        var definitions = LogMessageScanner.Definitions(SourceRoot);

        // Assert
        definitions.Count.ShouldBeGreaterThanOrEqualTo(35, "the scan must find the log messages it guards");
        definitions.Where(static definition => definition.EventId is null || definition.EventName is null || !PascalCaseRegex().IsMatch(definition.EventName))
            .Select(static definition => $"{definition.File}: id {definition.EventId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"}, name {definition.EventName ?? "none"}")
            .ShouldBeEmpty();
        definitions.Where(static definition => definition.EventName is not null)
            .GroupBy(static definition => (definition.Package, definition.EventName))
            .Where(static group => group.Count() > 1)
            .Select(static group => $"{group.Key.Package}: {group.Key.EventName}")
            .ShouldBeEmpty();
    }

    [Fact]
    public void Every_event_ID_is_unique_across_shared_kernel_and_inside_its_package_block()
    {
        // Arrange
        var (blocks, _) = LogMessageScanner.DocumentedLogs(TelemetryDocument);

        // Act
        var definitions = LogMessageScanner.Definitions(SourceRoot).Where(static definition => definition.EventId is not null).ToList();

        // Assert
        blocks.Count.ShouldBeGreaterThanOrEqualTo(10, "docs/telemetry.md must list the event id block of every package that logs");
        definitions.GroupBy(static definition => definition.EventId)
            .Where(static group => group.Count() > 1)
            .Select(static group => $"{group.Key}: {string.Join(", ", group.Select(static definition => definition.File))}")
            .ShouldBeEmpty();
        definitions.Where(definition => !blocks.TryGetValue(definition.Package, out var block)
                || definition.EventId < block.Start
                || definition.EventId > block.End)
            .Select(static definition => $"{definition.File}: {definition.EventId}")
            .ShouldBeEmpty();
        blocks.Values.Where(static block => block.End - block.Start != 99 || block.Start % 100 != 0)
            .Select(static block => $"{block.Start}-{block.End}")
            .ShouldBeEmpty();
    }

    [Fact]
    public void Every_message_placeholder_is_pascal_case()
    {
        // Act
        var definitions = LogMessageScanner.Definitions(SourceRoot);

        // Assert
        definitions.SelectMany(static definition => definition.Placeholders
                .Where(static placeholder => !PascalCaseRegex().IsMatch(placeholder))
                .Select(placeholder => $"{definition.File}: {{{placeholder}}}"))
            .ShouldBeEmpty();
    }

    [Fact]
    public void The_telemetry_documentation_lists_every_log_event()
    {
        // Arrange
        var (_, documented) = LogMessageScanner.DocumentedLogs(TelemetryDocument);

        // Act
        var defined = LogMessageScanner.Definitions(SourceRoot)
            .Where(static definition => definition.EventId is not null && definition.EventName is not null)
            .Select(static definition => (definition.Package, EventId: definition.EventId!.Value, EventName: definition.EventName!))
            .ToHashSet();

        // Assert
        defined.Except(documented).Select(static entry => $"missing from docs: {entry}").ShouldBeEmpty();
        documented.Except(defined).Select(static entry => $"not in source: {entry}").ShouldBeEmpty();
    }

    [GeneratedRegex("""^[A-Z][A-Za-z0-9]*$""")]
    private static partial Regex PascalCaseRegex();
}
