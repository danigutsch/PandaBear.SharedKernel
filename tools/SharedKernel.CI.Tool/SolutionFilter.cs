using System.Text.Json;

namespace PandaBear.CI.Tool;

/// <summary>
/// Writes solution filters (<c>.slnf</c>), which <c>dotnet restore</c>, <c>dotnet build</c>, and <c>dotnet test --solution</c>
/// accept in place of a solution. They load only the listed projects and the projects those reference.
/// </summary>
internal static class SolutionFilter
{
    /// <summary>Writes a filter of <paramref name="solution"/> that lists <paramref name="projects"/>.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="solution">The repository-relative solution.</param>
    /// <param name="path">The filter file.</param>
    /// <param name="projects">Repository-relative projects of the solution.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <remarks>
    /// The solution path is relative to the filter's directory, and project paths are relative to the solution's
    /// directory, in ordinal order; both use <c>/</c>.
    /// </remarks>
    public static Task Write(string root, string solution, string path, IEnumerable<string> projects, CancellationToken ct)
    {
        var solutionPath = Path.GetFullPath(solution, root);
        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("solution");
            writer.WriteString("path", Path.GetRelativePath(Path.GetDirectoryName(path)!, solutionPath).Replace('\\', '/'));
            writer.WriteStartArray("projects");
            foreach (var project in projects
                .Select(project => Path.GetRelativePath(solutionDirectory, Path.GetFullPath(project, root)).Replace('\\', '/'))
                .Order(StringComparer.Ordinal))
            {
                writer.WriteStringValue(project);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        buffer.WriteByte((byte)'\n');
        return File.WriteAllBytesAsync(path, buffer.ToArray(), ct);
    }
}
