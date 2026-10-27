using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace PandaBear.RepoConfig.Tool;

/// <summary>Creates a GitHub dependency snapshot for a repository.</summary>
internal static class DependencySnapshotCommand
{
    private const int MaximumInputBytes = 64 * 1024 * 1024;
    /// <summary>Largest snapshot output size, in bytes.</summary>
    internal const int MaximumOutputBytes = 32 * 1024 * 1024;
    private const int MaximumEdgeVisits = 1_000_000;

    /// <summary>Validates the options, writes the dependency snapshot, and returns the exit code.</summary>
    public static async Task<int> Run(DependencySnapshotOptions options, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        try
        {
            var values = new[] { options.RootPath, options.Sha, options.Reference, options.Job, options.Correlator, options.OutputPath };
            if (values.Any(value => string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl)))
            {
                throw new ArgumentException("Snapshot arguments are required.");
            }

            var root = Path.GetFullPath(options.RootPath);
            var destination = Path.GetFullPath(options.OutputPath, root);
            RollbackFileWriteBatch.ValidateWritePath(root, destination);
            var sha = options.Sha;
            if (sha.Length != 40 || !sha.All(char.IsAsciiHexDigitLower)
                || !options.Reference.StartsWith("refs/", StringComparison.Ordinal))
            {
                throw new ArgumentException("Snapshot commit and ref are invalid.");
            }

            var git = new RoadmapGitRepository();
            if (await git.ReadHead(root, cancellationToken).ConfigureAwait(false) != sha
                || (await git.ReadStatus(root, cancellationToken).ConfigureAwait(false)).Length != 0)
            {
                throw new InvalidOperationException("Snapshot inputs must be the exact clean reviewed commit.");
            }

            var content = await Build(root, sha, options.Reference, options.Job, options.Correlator, git, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            RollbackFileWriteBatch.ValidateWritePath(root, destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            RollbackFileWriteBatch.Apply(root, [new AtomicFileWrite(destination, content, null)]);
            await output.WriteLineAsync("Dependency snapshot generated from committed lockfiles without package restore.").ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or JsonException
            or InvalidOperationException or UnauthorizedAccessException or NotSupportedException or XmlException or KeyNotFoundException)
        {
            await error.WriteLineAsync("dependency snapshot: Invalid arguments, unsafe paths, incomplete committed dependency inputs, or dependency size limits exceeded.").ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<string> Build(string root, string sha, string reference, string job, string correlator, RoadmapGitRepository git, CancellationToken cancellationToken)
    {
        var solutions = (await git.ReadCommittedRootPaths(root, sha, cancellationToken).ConfigureAwait(false))
            .Where(path => path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetFullPath(path, root))
            .ToArray();
        if (solutions.Length != 1)
        {
            throw new InvalidOperationException("Exactly one solution is required.");
        }

        var remainingBytes = MaximumInputBytes;
        var solutionBytes = await ReadInput(root, solutions[0], sha, git, remainingBytes, cancellationToken).ConfigureAwait(false);
        remainingBytes -= solutionBytes.Length;
        using var solutionStream = new MemoryStream(solutionBytes, writable: false);
        using var reader = XmlReader.Create(solutionStream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var solution = XDocument.Load(reader);
        var projects = solution.Descendants("Project")
            .Select(project => project.Attribute("Path")?.Value ?? throw new InvalidOperationException("Project path is required."))
            .Select(path => Path.GetFullPath(path.Replace('\\', '/'), root))
            .ToArray();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (projects.Length == 0 || projects.Distinct(comparer).Count() != projects.Length)
        {
            throw new InvalidOperationException("Solution projects must be nonempty and unique.");
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", 0);
            writer.WriteString("sha", sha);
            writer.WriteString("ref", reference);
            writer.WriteString("scanned", TimeProvider.System.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
            writer.WriteStartObject("job");
            writer.WriteString("id", job);
            writer.WriteString("correlator", correlator);
            writer.WriteEndObject();
            writer.WriteStartObject("detector");
            writer.WriteString("name", "sharedkernel-nuget-lockfiles");
            writer.WriteString("version", "1.0.0");
            writer.WriteString("url", "https://github.com/danigutsch/PandaBear.SharedKernel");
            writer.WriteEndObject();
            writer.WriteStartObject("manifests");
            ValidateOutputBudget(writer);
            HashSet<string> locks = new(comparer);
            var remainingEdgeVisits = MaximumEdgeVisits;
            foreach (var project in projects.Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var projectBytes = await ReadInput(root, project, sha, git, remainingBytes, cancellationToken).ConfigureAwait(false);
                remainingBytes -= projectBytes.Length;
                var lockPath = Path.Combine(Path.GetDirectoryName(project)!, "packages.lock.json");
                if (!locks.Add(lockPath))
                {
                    throw new InvalidOperationException("Each solution project must have its own lockfile.");
                }

                var bytes = await ReadInput(root, lockPath, sha, git, remainingBytes, cancellationToken).ConfigureAwait(false);
                remainingBytes -= bytes.Length;
                using var document = JsonDocument.Parse(bytes);
                var value = document.RootElement;
                RequireObject(value);
                if (!value.GetProperty("version").TryGetInt32(out var version) || version is < 1 or > 2)
                {
                    throw new InvalidOperationException("Unsupported lockfile version.");
                }

                var frameworks = value.GetProperty("dependencies");
                RequireObject(frameworks);
                if (!frameworks.EnumerateObject().Any())
                {
                    throw new InvalidOperationException("A lockfile must declare its target frameworks.");
                }

                foreach (var framework in frameworks.EnumerateObject())
                {
                    WriteFramework(writer, Path.GetRelativePath(root, lockPath).Replace('\\', '/'), framework, ref remainingEdgeVisits);
                }
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
            ValidateOutputBudget(writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray()) + Environment.NewLine;
    }

    private static async Task<byte[]> ReadInput(string root, string path, string revision, RoadmapGitRepository git, int remainingBytes, CancellationToken cancellationToken)
    {
        RollbackFileWriteBatch.ValidateWritePath(root, path);
        var relativePath = Path.GetRelativePath(root, path).Replace('\\', '/');
        var bytes = await git.ReadCommittedFile(root, revision, relativePath, cancellationToken).ConfigureAwait(false);
        if (bytes.Length > remainingBytes)
        {
            throw new InvalidOperationException("Dependency input budget exceeded.");
        }

        return bytes;
    }

    private static void WriteFramework(Utf8JsonWriter writer, string path, JsonProperty framework, ref int remainingEdgeVisits)
    {
        RequireObject(framework.Value);
        var nodes = framework.Value.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> identifiers = new(StringComparer.OrdinalIgnoreCase);
        Span<byte> decodedHash = stackalloc byte[64];
        foreach (var (name, node) in nodes)
        {
            RequireObject(node);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Dependency name is required.");
            }

            var kind = node.GetProperty("type").GetString();
            if (kind is not ("Direct" or "Transitive" or "CentralTransitive" or "Project"))
            {
                throw new InvalidOperationException("Unsupported dependency type.");
            }

            if (kind != "Project")
            {
                var version = node.GetProperty("resolved").GetString();
                var hash = node.GetProperty("contentHash").GetString();
                if (string.IsNullOrWhiteSpace(version) || version.Any(char.IsControl)
                    || hash is null || !Convert.TryFromBase64String(hash, decodedHash, out var decodedBytes) || decodedBytes != 64)
                {
                    throw new InvalidOperationException("Resolved package version and content hash are required.");
                }

                identifiers.Add(name, "pkg:nuget/" + Uri.EscapeDataString(name) + "@" + Uri.EscapeDataString(version));
            }
        }

        var edges = nodes.ToDictionary(pair => pair.Key, pair => ReadEdges(pair.Value), StringComparer.OrdinalIgnoreCase);
        foreach (var targets in edges.Values)
        {
            if (targets.Any(target => !nodes.ContainsKey(target)))
            {
                throw new InvalidOperationException("A dependency edge has no resolved target.");
            }
        }

        var projectNames = nodes.Keys.Where(name => !identifiers.ContainsKey(name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ValidateProjectCycles(edges, projectNames);

        writer.WriteStartObject(path + "#" + framework.Name);
        writer.WriteString("name", path + " (" + framework.Name + ")");
        writer.WriteStartObject("file");
        writer.WriteString("source_location", path);
        writer.WriteEndObject();
        writer.WriteStartObject("metadata");
        writer.WriteString("target_framework", framework.Name);
        writer.WriteEndObject();
        writer.WriteStartObject("resolved");
        ValidateOutputBudget(writer);
        foreach (var (name, node) in nodes.Where(pair => identifiers.ContainsKey(pair.Key)).OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var identifier = identifiers[name];
            writer.WriteStartObject(identifier);
            var kind = node.GetProperty("type").GetString();
            writer.WriteString("package_url", identifier);
            writer.WriteString("relationship", kind == "Direct" ? "direct" : "indirect");

            writer.WriteString("scope", "runtime");
            writer.WriteStartObject("metadata");
            writer.WriteString("nuget_type", kind);
            writer.WriteEndObject();
            writer.WriteStartArray("dependencies");
            ValidateOutputBudget(writer);
            foreach (var target in PackageTargets(name, edges, identifiers, ref remainingEdgeVisits))
            {
                writer.WriteStringValue(target);
                ValidateOutputBudget(writer);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            ValidateOutputBudget(writer);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static string[] ReadEdges(JsonElement node)
    {
        if (!node.TryGetProperty("dependencies", out var dependencies))
        {
            return [];
        }

        RequireObject(dependencies);
        if (dependencies.EnumerateObject().Any(edge => edge.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(edge.Value.GetString())))
        {
            throw new InvalidOperationException("Dependency edge constraints must be strings.");
        }

        return dependencies.EnumerateObject().Select(edge => edge.Name).ToArray();
    }

    private static SortedSet<string> PackageTargets(string name, Dictionary<string, string[]> edges, Dictionary<string, string> identifiers, ref int remainingEdgeVisits)
    {
        SortedSet<string> targets = new(StringComparer.Ordinal);
        HashSet<string> projects = new(StringComparer.OrdinalIgnoreCase);
        Stack<string> pending = new(edges[name]);
        while (pending.TryPop(out var target))
        {
            if (--remainingEdgeVisits < 0)
            {
                throw new InvalidOperationException("Dependency expansion budget exceeded.");
            }

            if (identifiers.TryGetValue(target, out var packageUrl))
            {
                targets.Add(packageUrl);
            }
            else if (projects.Add(target))
            {
                foreach (var child in edges[target])
                {
                    pending.Push(child);
                }
            }
        }

        return targets;
    }

    private static void ValidateProjectCycles(Dictionary<string, string[]> edges, HashSet<string> projects)
    {
        var remaining = projects.ToDictionary(name => name, name => edges[name].Count(projects.Contains), StringComparer.OrdinalIgnoreCase);
        var parents = projects.ToDictionary(name => name, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var project in projects)
        {
            foreach (var child in edges[project].Where(projects.Contains))
            {
                parents[child].Add(project);
            }
        }

        Queue<string> ready = new(remaining.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var count = 0;
        while (ready.TryDequeue(out var project))
        {
            count++;
            foreach (var parent in parents[project])
            {
                if (--remaining[parent] == 0)
                {
                    ready.Enqueue(parent);
                }
            }
        }

        if (count != projects.Count)
        {
            throw new InvalidOperationException("Local project dependency cycles are not supported.");
        }
    }

    private static void ValidateOutputBudget(Utf8JsonWriter writer)
    {
        if (writer.BytesCommitted + writer.BytesPending > MaximumOutputBytes)
        {
            throw new InvalidOperationException("Dependency snapshot output budget exceeded.");
        }
    }

    private static void RequireObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || element.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != element.EnumerateObject().Count())
        {
            throw new InvalidOperationException("Dependency objects must contain unique property names.");
        }
    }
}
