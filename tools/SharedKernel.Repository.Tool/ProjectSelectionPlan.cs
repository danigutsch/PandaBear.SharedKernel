using System.Text;
using System.Text.Json;

namespace PandaBear.Repository.Tool;

/// <summary>Writes the validation plan for the projects affected since a base revision.</summary>
internal static class ProjectSelectionPlan
{
    public static async Task Write(
        string root,
        string directory,
        string? baseRevision,
        string? githubOutput,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        TextWriter output,
        TextWriter error,
        CancellationToken ct)
    {
        string[]? changes = null;
        if (baseRevision is not null)
        {
            try
            {
                var diff = await run("git", ["diff", "--name-only", "--no-renames", baseRevision + "...HEAD", "--"], root, ct).ConfigureAwait(false);
                changes = diff.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
            catch (InvalidOperationException)
            {
                // Missing history deliberately selects full validation.
            }
        }

        var result = ProjectSelector.Select(root, changes);
        Directory.CreateDirectory(directory);
        var revision = "unavailable";
        try
        {
            revision = (await run("git", ["rev-parse", "HEAD"], root, ct).ConfigureAwait(false)).Trim();
        }
        catch (InvalidOperationException)
        {
            await error.WriteLineAsync("Selection revision unavailable; execution requires a Git checkout.").ConfigureAwait(false);
        }

        await File.WriteAllTextAsync(Path.Combine(directory, "revision.txt"), revision, ct).ConfigureAwait(false);
        await File.WriteAllLinesAsync(Path.Combine(directory, "build-projects.txt"), result.BuildProjects, ct).ConfigureAwait(false);
        await File.WriteAllLinesAsync(Path.Combine(directory, "test-projects.txt"), result.TestProjects, ct).ConfigureAwait(false);
        await File.WriteAllLinesAsync(Path.Combine(directory, "selection-reasons.txt"), result.Reasons, ct).ConfigureAwait(false);
        await File.WriteAllBytesAsync(Path.Combine(directory, "selection.json"), SelectionJson(revision, baseRevision, result), ct).ConfigureAwait(false);
        var candidateRequired = result.BuildProjects.Length > 0 ? "true" : "false";
        await File.WriteAllTextAsync(Path.Combine(directory, "candidate-required.txt"), candidateRequired, ct).ConfigureAwait(false);
        if (githubOutput is not null)
        {
            var hosted = result.TestProjects.Where(project => RepositoryValidation.IsHosted(root, project)).ToArray();
            await File.AppendAllTextAsync(
                Path.GetFullPath(githubOutput, root),
                "candidate_required=" + candidateRequired + Environment.NewLine
                    + "hosted_required=" + (hosted.Length > 0 ? "true" : "false") + Environment.NewLine
                    + "hosted_matrix=" + HostedMatrix(hosted) + Environment.NewLine,
                ct).ConfigureAwait(false);
        }

        await output.WriteLineAsync(string.Join(Environment.NewLine, result.Reasons)).ConfigureAwait(false);
    }

    private static byte[] SelectionJson(string revision, string? baseRevision, ProjectSelection result)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("revision", revision);
            writer.WriteString("comparisonBase", baseRevision);
            WriteArray(writer, "buildProjects", result.BuildProjects);
            WriteArray(writer, "testProjects", result.TestProjects);
            WriteArray(writer, "reasons", result.Reasons);
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    private static void WriteArray(Utf8JsonWriter writer, string name, string[] values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static string HostedMatrix(string[] hosted)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var project in hosted)
            {
                writer.WriteStartObject();
                writer.WriteString("project", project);
                writer.WriteString("name", Path.GetFileNameWithoutExtension(project));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
