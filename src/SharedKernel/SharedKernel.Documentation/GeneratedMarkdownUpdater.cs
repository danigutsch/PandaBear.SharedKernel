using System.Text;
using System.Text.RegularExpressions;

namespace SharedKernel.Documentation;

/// <summary>
/// Updates named generated blocks inside Markdown documents.
/// </summary>
internal sealed class GeneratedMarkdownUpdater(string rootPath, string docsRelativePath, string generatorName)
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromSeconds(1);
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private readonly string fullDocsPath = Path.GetFullPath(docsRelativePath, Path.GetFullPath(rootPath));
    private readonly string fullRootPath = Path.GetFullPath(rootPath);

    /// <summary>
    /// Updates generated blocks and returns repository-relative files whose content changed.
    /// </summary>
    /// <param name="checkOnly">Whether to detect drift without writing files.</param>
    /// <param name="replacements">Generated block names, target paths, and replacement content.</param>
    /// <returns>Repository-relative file paths changed or stale.</returns>
    public List<string> Update(
        bool checkOnly,
        IReadOnlyList<(string Name, string TargetPath, string Replacement)> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        ValidateDocsPath();

        var documents = MarkdownDocs().ToDictionary(
            file => file.FullName,
            file => File.ReadAllText(file.FullName, Utf8NoBom),
            PathComparer);
        var blocks = replacements
            .Select(replacement =>
            {
                if (replacement.Replacement.Contains("<!-- generated:", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Generated block '{replacement.Name}' replacement content must not contain generated markers.");
                }

                var targetPath = ResolveTargetPath(replacement.Name, replacement.TargetPath);
                if (!documents.ContainsKey(targetPath))
                {
                    throw new InvalidOperationException(
                        $"Generated block '{replacement.Name}' target '{RepositoryRelativePath(targetPath)}' "
                        + "must be an existing Markdown file within docsPath.");
                }

                return (replacement.Name, TargetPath: targetPath, replacement.Replacement);
            })
            .ToArray();

        foreach (var block in blocks)
        {
            ValidateMarkerOwnership(block.Name, block.TargetPath, documents);
        }

        var proposedDocuments = new Dictionary<string, string>(documents, PathComparer);
        foreach (var targetBlocks in blocks
                     .GroupBy(block => block.TargetPath, PathComparer)
                     .OrderBy(group => group.Key, PathComparer))
        {
            var original = documents[targetBlocks.Key];
            var updated = original;
            foreach (var block in targetBlocks)
            {
                updated = ReplaceGeneratedBlock(updated, block.Name, block.Replacement);
            }

            proposedDocuments[targetBlocks.Key] = updated;
        }

        foreach (var block in blocks)
        {
            ValidateMarkerOwnership(block.Name, block.TargetPath, proposedDocuments);
        }

        var changed = proposedDocuments
            .Where(document => document.Value != documents[document.Key])
            .OrderBy(document => document.Key, PathComparer)
            .Select(document => (Path: document.Key, Content: document.Value))
            .ToList();

        if (!checkOnly)
        {
            foreach (var update in changed)
            {
                WriteAtomically(update.Path, update.Content);
            }
        }

        return changed.Select(update => RepositoryRelativePath(update.Path)).ToList();
    }

    internal static void WriteAtomically(string path, string content)
    {
        var directoryPath = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Cannot determine the directory for '{path}'.");
        var temporaryPath = Path.Combine(
            directoryPath,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temporaryPath, content, Utf8NoBom);
            File.Replace(temporaryPath, path, destinationBackupFileName: null);
        }
        catch
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception cleanupException)
                when (cleanupException is IOException or UnauthorizedAccessException)
            {
                // Preserve the primary write or replacement exception.
            }

            throw;
        }
    }

    private IEnumerable<FileInfo> MarkdownDocs()
    {
        var pendingDirectories = new Stack<DirectoryInfo>();
        pendingDirectories.Push(new DirectoryInfo(fullDocsPath));
        var markdownFiles = new List<FileInfo>();

        while (pendingDirectories.TryPop(out var directory))
        {
            EnsureNotSymbolicLink(directory, "Documentation directory");

            foreach (var childDirectory in directory.EnumerateDirectories())
            {
                EnsureNotSymbolicLink(childDirectory, "Documentation directory");
                pendingDirectories.Push(childDirectory);
            }

            foreach (var file in directory.EnumerateFiles("*.md"))
            {
                EnsureNotSymbolicLink(file, "Markdown document");
                markdownFiles.Add(file);
            }
        }

        return markdownFiles.OrderBy(file => file.FullName, StringComparer.Ordinal);
    }

    private string ResolveTargetPath(string blockName, string targetRelativePath)
    {
        if (Path.IsPathRooted(targetRelativePath))
        {
            throw new InvalidOperationException(
                $"Generated block '{blockName}' targetPath '{targetRelativePath}' must be relative to docsPath '{docsRelativePath}'.");
        }

        var targetPath = Path.GetFullPath(targetRelativePath, fullDocsPath);
        var relativePath = Path.GetRelativePath(fullDocsPath, targetPath);
        if (EscapesBasePath(relativePath))
        {
            throw new InvalidOperationException(
                $"Generated block '{blockName}' targetPath '{targetRelativePath}' must stay within docsPath '{docsRelativePath}'.");
        }

        return targetPath;
    }

    private void ValidateDocsPath()
    {
        var relativePath = Path.GetRelativePath(fullRootPath, fullDocsPath);
        if (EscapesBasePath(relativePath))
        {
            throw new InvalidOperationException(
                $"Configured docsPath '{docsRelativePath}' must stay within the repository root.");
        }

        var currentPath = fullRootPath;
        foreach (var segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            FileSystemInfo fileSystemInfo = Directory.Exists(currentPath)
                ? new DirectoryInfo(currentPath)
                : new FileInfo(currentPath);
            EnsureNotSymbolicLink(fileSystemInfo, "Configured docsPath");
        }
    }

    private void ValidateMarkerOwnership(
        string name,
        string targetPath,
        Dictionary<string, string> documents)
    {
        var startMarker = $"<!-- generated:{name}:start -->";
        var endMarker = $"<!-- generated:{name}:end -->";
        var targetText = documents[targetPath];
        var startCount = CountOccurrences(targetText, startMarker);
        var endCount = CountOccurrences(targetText, endMarker);
        var startIndex = targetText.IndexOf(startMarker, StringComparison.Ordinal);
        var endIndex = targetText.IndexOf(endMarker, StringComparison.Ordinal);
        var outsideMarkerCount = documents
            .Where(document => !PathComparer.Equals(document.Key, targetPath))
            .Sum(document =>
                CountOccurrences(document.Value, startMarker) + CountOccurrences(document.Value, endMarker));

        if (startCount == 1 && endCount == 1 && startIndex < endIndex && outsideMarkerCount == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Generated block '{name}' requires exactly one ordered marker pair in target "
            + $"'{RepositoryRelativePath(targetPath)}'; found {startCount} start, {endCount} end, "
            + $"and {outsideMarkerCount} marker(s) outside the target.");
    }

    private string ReplaceGeneratedBlock(string text, string name, string replacement)
    {
        var pattern = new Regex(
            $"<!-- generated:{Regex.Escape(name)}:start -->.*?<!-- generated:{Regex.Escape(name)}:end -->",
            RegexOptions.Singleline | RegexOptions.CultureInvariant,
            RegexMatchTimeout);
        var block = string.Join(
            '\n',
            $"<!-- generated:{name}:start -->",
            $"<!-- Generated by {generatorName}. Do not edit by hand. -->",
            string.Empty,
            replacement.TrimEnd(),
            $"<!-- generated:{name}:end -->");

        return pattern.Replace(text, _ => block);
    }

    private string RepositoryRelativePath(string path) =>
        Path.GetRelativePath(fullRootPath, path).Replace(Path.DirectorySeparatorChar, '/');

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while (offset < text.Length)
        {
            var index = text.IndexOf(value, offset, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            offset = index + value.Length;
        }

        return count;
    }

    private static bool EscapesBasePath(string relativePath) =>
        Path.IsPathRooted(relativePath)
        || relativePath.Equals("..", StringComparison.Ordinal)
        || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static void EnsureNotSymbolicLink(FileSystemInfo fileSystemInfo, string description)
    {
        if (fileSystemInfo.LinkTarget is not null)
        {
            throw new InvalidOperationException(
                $"{description} '{fileSystemInfo.FullName}' must not be a symbolic link or junction.");
        }
    }
}
