using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SharedKernel.IO;

namespace SharedKernel.CodeFixes.MSBuild;

/// <summary>Publishes changes that add source files without asking Roslyn to add unconditional MSBuild items.</summary>
internal static class CodeFixFileChanges
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Applies document edits and adds only the compile items that the target projects require.</summary>
    /// <exception cref="InvalidOperationException">A source path is missing or an evaluated build context cannot compile the helper once.</exception>
    /// <exception cref="IOException">A destination is not writable or a new source file already exists.</exception>
    /// <exception cref="AggregateException">An apply failure is followed by a failure to restore the files.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was canceled before source publication.</exception>
    public static Task<Solution> Apply(Solution before, Solution after, CancellationToken ct) =>
        Apply(before, after, CompileMembership.FindMissingItem, ct);

    /// <inheritdoc cref="Apply(Solution, Solution, CancellationToken)"/>
    internal static async Task<Solution> Apply(
        Solution before,
        Solution after,
        Func<string, string, XNamespace, CancellationToken, Task<XElement?>> findMissingItem,
        CancellationToken ct)
    {
        var additions = new List<(string Project, string Path, SourceText Text)>();
        var updates = new Dictionary<string, (SourceText Before, SourceText After)>(StringComparer.Ordinal);
        foreach (var project in after.GetChanges(before).GetProjectChanges())
        {
            foreach (var id in project.GetAddedDocuments())
            {
                var document = after.GetDocument(id)!;
                var projectPath = document.Project.FilePath ?? throw new InvalidOperationException("A new source file has no project path.");
                var path = Path.GetFullPath(Path.Combine([Path.GetDirectoryName(projectPath)!, .. document.Folders, document.Name]));
                var text = await document.GetTextAsync(ct).ConfigureAwait(false);
                additions.Add((projectPath, path, text));
                after = after.WithDocumentFilePath(id, path);
            }

            foreach (var id in project.GetChangedDocuments())
            {
                var document = after.GetDocument(id)!;
                var path = document.FilePath ?? throw new InvalidOperationException("A changed source file has no path.");
                var text = await document.GetTextAsync(ct).ConfigureAwait(false);
                var previous = await before.GetDocument(id)!.GetTextAsync(ct).ConfigureAwait(false);
                updates.TryAdd(path, (previous, text));
                foreach (var relatedId in after.GetDocumentIdsWithFilePath(path))
                {
                    after = after.WithDocumentText(relatedId, text);
                }
            }
        }

        var undo = new Stack<Func<ValueTask>>();
        try
        {
            foreach (var addition in additions)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(addition.Path)!);
                if (!await AtomicFile.CreateIfAbsent(addition.Path, (stream, token) => Write(stream, addition.Text, token), ct).ConfigureAwait(false))
                {
                    throw new IOException($"The new source file already exists: {addition.Path}");
                }

                undo.Push(() => Delete(addition.Path));
            }

            foreach (var addition in additions)
            {
                await UpdateProject(addition.Project, addition.Path, findMissingItem, undo, ct).ConfigureAwait(false);
            }

            // Honor cancellation before replacing source. Complete an in-progress publication before observing it again.
            ct.ThrowIfCancellationRequested();
            foreach (var (path, (previous, text)) in updates)
            {
                await Replace(path, text, CancellationToken.None).ConfigureAwait(false);
                undo.Push(() => Replace(path, previous, CancellationToken.None));
            }

            return after;
        }
        catch (Exception failure)
        {
            List<Exception> rollbackErrors = [];
            while (undo.TryPop(out var restore))
            {
                try
                {
                    await restore().ConfigureAwait(false);
                }
                catch (Exception rollbackError) when (rollbackError is IOException or UnauthorizedAccessException)
                {
                    rollbackErrors.Add(rollbackError);
                }
            }

            if (rollbackErrors.Count > 0)
            {
                throw new AggregateException("The file changes could not be fully restored.", [failure, .. rollbackErrors]);
            }

            throw;
        }
    }

    private static async Task UpdateProject(
        string projectPath,
        string sourcePath,
        Func<string, string, XNamespace, CancellationToken, Task<XElement?>> findMissingItem,
        Stack<Func<ValueTask>> undo,
        CancellationToken ct)
    {
        SourceText original;
        using (var stream = File.OpenRead(projectPath))
        {
            original = SourceText.From(stream);
        }

        var xml = XDocument.Parse(original.ToString(), LoadOptions.PreserveWhitespace);
        var root = xml.Root ?? throw new InvalidOperationException($"The project file has no root: {projectPath}");
        var item = await findMissingItem(projectPath, sourcePath, root.Name.Namespace, ct).ConfigureAwait(false);
        if (item is null)
        {
            return;
        }

        var eol = original.ToString().Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        root.Add(new XText("  "), item, new XText(eol));
        var markup = xml.ToString(SaveOptions.DisableFormatting);
        if (xml.Declaration is { } declaration)
        {
            markup = declaration + eol + markup;
        }

        await Replace(projectPath, SourceText.From(markup, original.Encoding), ct).ConfigureAwait(false);
        undo.Push(() => Replace(projectPath, original, CancellationToken.None));
        await CompileMembership.Verify(projectPath, sourcePath, ct).ConfigureAwait(false);
    }

    private static ValueTask Replace(string path, SourceText text, CancellationToken ct)
    {
        var destination = File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? path;
        return AtomicFile.Replace(destination, (stream, token) => Write(stream, text, token), ct);
    }

    private static async ValueTask Write(Stream stream, SourceText text, CancellationToken ct)
    {
        var writer = new StreamWriter(stream, text.Encoding ?? Utf8, leaveOpen: true);
        await using var writerScope = writer.ConfigureAwait(false);
        text.Write(writer, ct);
        await writer.FlushAsync(ct).ConfigureAwait(false);
    }

    private static ValueTask Delete(string path)
    {
        File.Delete(path);
        return ValueTask.CompletedTask;
    }
}
