using System.Globalization;
using System.Text;
using System.Text.Json;
using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>
/// Writes the evidence of a <c>run</c> to its execution directory: <c>phase-timings.tsv</c>, <c>manifest.json</c>, and
/// <c>diagnostics.txt</c>, and appends the phases to the GitHub step summary when one is given.
/// </summary>
internal static class RunEvidence
{
    public static async Task Write(
        string execution,
        string name,
        string revision,
        string[] slices,
        string[] projects,
        SliceRunOptions options,
        string status,
        string? failure,
        DateTimeOffset started,
        DateTimeOffset completed,
        List<RunPhase> phases,
        string root)
    {
        var total = completed - started;
        var timings = new StringBuilder("phase\tstarted_at_utc\tduration_seconds\tstatus\n");
        foreach (var phase in phases)
        {
            timings.Append(phase.Name).Append('\t').Append(Utc(phase.StartedAt)).Append('\t').Append(Seconds(phase.Duration)).Append('\t').Append(phase.Status).Append('\n');
        }

        timings.Append("total\t").Append(Utc(started)).Append('\t').Append(Seconds(total)).Append('\t').Append(status).Append('\n');
        await File.WriteAllTextAsync(Path.Combine(execution, "phase-timings.tsv"), timings.ToString(), CancellationToken.None).ConfigureAwait(false);

        using (var buffer = new MemoryStream())
        {
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
            {
                writer.WriteStartObject();
                writer.WriteString("revision", revision);
                writer.WriteString("status", status);
                Strings(writer, "slices", slices);
                writer.WriteString("configuration", options.Configuration);
                writer.WriteBoolean("coverage", options.Coverage);
                writer.WriteBoolean("installPlaywright", options.InstallPlaywright);
                Strings(writer, "projects", projects);
                writer.WriteString("startedAtUtc", Utc(started));
                writer.WriteString("completedAtUtc", Utc(completed));
                writer.WriteStartArray("phases");
                foreach (var phase in phases)
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", phase.Name);
                    writer.WriteString("status", phase.Status);
                    writer.WriteString("startedAtUtc", Utc(phase.StartedAt));
                    writer.WriteNumber("durationSeconds", Math.Round(phase.Duration.TotalSeconds, 3));
                    writer.WriteString("log", phase.Log);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            buffer.WriteByte((byte)'\n');
            await File.WriteAllBytesAsync(Path.Combine(execution, "manifest.json"), buffer.ToArray(), CancellationToken.None).ConfigureAwait(false);
        }

        // Failure messages carry process output, so each line is escaped.
        var diagnostics = new StringBuilder("sharedkernel-ci run diagnostics\n\n")
            .Append("Revision: ").Append(revision).Append('\n')
            .Append("Slices: ").Append(string.Join(", ", slices)).Append('\n')
            .Append("Status: ").Append(status).Append('\n')
            .Append("Duration: ").Append(Seconds(total)).Append(" s\n");
        if (failure is not null)
        {
            diagnostics.Append("\nFailure:\n");
            foreach (var line in failure.TrimEnd('\r', '\n').Split('\n'))
            {
                diagnostics.Append("  ").Append(ControlCharacters.Escape(line.TrimEnd('\r'))).Append('\n');
            }
        }

        diagnostics.Append("\nPhases:\n");
        foreach (var phase in phases)
        {
            diagnostics.Append("  ").Append(phase.Name).Append(' ').Append(phase.Status).Append(' ').Append(Seconds(phase.Duration)).Append(" s ").Append(phase.Log).Append('\n');
        }

        diagnostics.Append("\nFiles:\n");
        foreach (var file in Directory.EnumerateFiles(execution, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(execution, file).Replace('\\', '/'))
            .Where(static file => file != "diagnostics.txt")
            .Order(StringComparer.Ordinal))
        {
            diagnostics.Append("  ").Append(ControlCharacters.Escape(file)).Append('\n');
        }

        await File.WriteAllTextAsync(Path.Combine(execution, "diagnostics.txt"), diagnostics.ToString(), CancellationToken.None).ConfigureAwait(false);

        if (options.GithubSummary is { } summaryPath)
        {
            var summary = new StringBuilder("## sharedkernel-ci run ").Append(name).Append(": ").Append(status).Append("\n\n| Phase | Status | Seconds |\n| --- | --- | --- |\n");
            foreach (var phase in phases)
            {
                summary.Append("| ").Append(phase.Name).Append(" | ").Append(phase.Status).Append(" | ").Append(phase.Duration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)).Append(" |\n");
            }

            await File.AppendAllTextAsync(Path.GetFullPath(summaryPath, root), summary.Append('\n').ToString(), CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static void Strings(Utf8JsonWriter writer, string name, string[] values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static string Utc(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static string Seconds(TimeSpan value) => value.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture);
}
