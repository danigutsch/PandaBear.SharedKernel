using System.Xml;
using System.Xml.Linq;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// The tracked project, <c>Directory.Build.props</c>, and <c>Directory.Build.targets</c> files of a repository, read and
/// parsed once.
/// </summary>
internal sealed class MsBuildFileSet
{
    /// <summary>
    /// The largest MSBuild file, in bytes, that the tool reads.
    /// </summary>
    public const int MaxFileBytes = 4 * 1024 * 1024;

    private readonly Dictionary<string, XElement> _roots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _failures = new(StringComparer.Ordinal);

    private MsBuildFileSet()
    {
    }

    /// <summary>
    /// Reads and parses each file under <paramref name="root"/>. A file that is missing, too large, or not XML is
    /// remembered as a failure instead of failing the read.
    /// </summary>
    /// <param name="root">The repository root directory.</param>
    /// <param name="paths">The repository-relative paths of the files to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static async Task<MsBuildFileSet> Read(string root, IEnumerable<string> paths, CancellationToken cancellationToken)
    {
        MsBuildFileSet files = new();
        foreach (var path in paths)
        {
            await files.Add(root, path, cancellationToken).ConfigureAwait(false);
        }

        return files;
    }

    /// <summary>
    /// Returns the parsed root element of a file, or <see langword="null"/> and the reason in <paramref name="failure"/>.
    /// </summary>
    public XElement? Find(string path, out string? failure)
    {
        failure = _failures.GetValueOrDefault(path);
        return _roots.GetValueOrDefault(path);
    }

    /// <summary>
    /// Returns the path of the nearest file called <paramref name="fileName"/> in <paramref name="directory"/> or a
    /// parent of it, or <see langword="null"/> when no read file is there.
    /// </summary>
    /// <param name="directory">A repository-relative directory; an empty string is the root.</param>
    /// <param name="fileName">The file name to look for.</param>
    public string? FindAbove(string directory, string fileName)
    {
        var current = directory;
        while (true)
        {
            var candidate = current.Length == 0 ? fileName : current + "/" + fileName;
            if (_roots.ContainsKey(candidate) || _failures.ContainsKey(candidate))
            {
                return candidate;
            }

            if (current.Length == 0)
            {
                return null;
            }

            var separator = current.LastIndexOf('/');
            current = separator < 0 ? string.Empty : current[..separator];
        }
    }

    private async Task Add(string root, string path, CancellationToken cancellationToken)
    {
        byte[]? content;
        try
        {
            content = await RepositoryFile.Read(Path.Combine(root, path), MaxFileBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            _failures[path] = "Is missing from the working tree.";
            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _failures[path] = "Cannot be read.";
            return;
        }

        if (content is null)
        {
            _failures[path] = "Exceeds the 4 MiB project file size limit.";
            return;
        }

        try
        {
            using MemoryStream stream = new(content);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            _roots[path] = XDocument.Load(reader).Root ?? throw new XmlException();
        }
        catch (XmlException)
        {
            _failures[path] = "Is not valid XML.";
        }
    }
}
