using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using PandaBear.Tooling;

namespace PandaBear.Versioning.Tool;

/// <summary>
/// Runs a consumer repository's command on a clean clone of its committed revision, with a candidate's packages staged
/// in the clone's own SharedKernel package folder, and records the result as consumer compatibility evidence.
/// </summary>
internal static class ConsumerVerificationCommand
{
    /// <summary>The default evidence path, relative to the repository root.</summary>
    public const string DefaultOutput = "TestResults/consumer-compatibility.json";

    private const string PackagePrefix = "PandaBear.SharedKernel.";

    private const string PackagePattern = PackagePrefix + "*";

    private const int MaxReasonLength = 4096;

    /// <summary>
    /// Verifies the candidate and the consumer, runs the command, and writes the evidence with <c>passed</c>,
    /// <c>failed</c>, or <c>pending</c>. A canceled run writes no evidence. The scratch directory is always deleted.
    /// </summary>
    /// <exception cref="ArgumentException">The evidence file would replace the candidate manifest.</exception>
    /// <exception cref="InvalidOperationException">The evidence is not <c>passed</c>; the message gives the reason.</exception>
    public static async Task Run(ConsumerVerificationOptions options, TextWriter output, TextWriter error, CancellationToken ct)
    {
        if (string.Equals(options.Output, options.Candidate, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("--output names the candidate manifest, which keeps the evidence that the library recorded.");
        }

        var evidence = new JsonObject();
        string? revision = null;
        bool? dirty = null;
        var status = "failed";
        Exception? failure = null;
        var scratch = Path.Combine(Path.GetTempPath(), "sharedkernel-consumer-" + Guid.NewGuid().ToString("N"));
        try
        {
            try
            {
                revision = (await CommandRunner.Run("git", ["rev-parse", "HEAD"], options.RepoRoot, ct).ConfigureAwait(false)).Trim();
                dirty = (await CommandRunner.Run("git", ["status", "--porcelain"], options.RepoRoot, ct).ConfigureAwait(false)).Length != 0;
                evidence = CandidateManifest.ReadAndVerify(options.Candidate);
                var version = evidence["packageVersion"]?.GetValue<string>() ?? throw new ArgumentException("Candidate package version is unavailable.");
                _ = SemanticVersion.Parse(version);
                var libraryDirty = evidence["libraryDirty"]?.GetValue<bool>() ?? throw new ArgumentException("Candidate library state is unavailable.");
                if (libraryDirty || dirty.Value)
                {
                    status = "pending";
                    throw new ArgumentException("Compatibility requires committed library and consumer revisions; commit the changes and run again.");
                }

                var clone = Path.Combine(scratch, "consumer");
                Directory.CreateDirectory(scratch);
                await CommandRunner.Run("git", ["clone", "--quiet", "--no-checkout", "--shared", "--", options.RepoRoot, clone], scratch, ct).ConfigureAwait(false);
                await CommandRunner.Run("git", ["checkout", "--quiet", "--detach", revision], clone, ct).ConfigureAwait(false);
                ValidateConsumer(clone, Solution(clone, options), version);
                await StagePackages(clone, options.Candidate, evidence["packageSha256"]!.AsObject(), ct).ConfigureAwait(false);
                var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["NUGET_PACKAGES"] = Path.Combine(scratch, "packages"),
                    ["NUGET_HTTP_CACHE_PATH"] = Path.Combine(scratch, "http-cache"),
                    ["NUGET_FALLBACK_PACKAGES"] = null,
                };
                await CommandRunner.RunStreaming(options.Command[0], options.Command[1..], clone, environment, error, ct).ConfigureAwait(false);
                status = "passed";
            }
            catch (Exception exception) when (!ct.IsCancellationRequested && exception is ArgumentException
                or InvalidOperationException
                or IOException
                or UnauthorizedAccessException
                or JsonException
                or XmlException)
            {
                failure = exception;
            }

            if (revision is not null)
            {
                evidence["consumerRevision"] = revision;
            }

            if (dirty is not null)
            {
                evidence["consumerDirty"] = dirty.Value;
            }

            evidence["consumerCompatibility"] = status;
            evidence.Remove("reason");
            if (failure is not null)
            {
                evidence["reason"] = failure.Message.Length <= MaxReasonLength ? failure.Message : failure.Message[..MaxReasonLength] + " (truncated)";
            }

            CandidateManifest.Write(options.Output, evidence);
            await output.WriteLineAsync("Consumer compatibility: " + status + ". Evidence: " + options.Output).ConfigureAwait(false);
            if (failure is not null)
            {
                throw new InvalidOperationException(failure.Message, failure);
            }
        }
        finally
        {
            DeleteScratch(scratch, error);
        }
    }

    private static string Solution(string clone, ConsumerVerificationOptions options)
    {
        if (options.Solution is null)
        {
            var solutions = Directory.GetFiles(clone, "*.slnx");
            return solutions.Length == 1
                ? solutions[0]
                : throw new ArgumentException($"The repository root has {solutions.Length} .slnx files; name one with --solution.");
        }

        var relative = Path.GetRelativePath(options.RepoRoot, options.Solution).Replace('\\', '/');
        return RepositoryPath.IsRelative(relative) && relative.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
            ? RepositoryPath.Resolve(clone, relative)
            : throw new ArgumentException("--solution must name a .slnx file inside the repository: " + options.Solution + ".");
    }

    /// <summary>
    /// Requires every <c>PandaBear.SharedKernel.*</c> central package version, lock file entry, and local tool, compared
    /// case-insensitively, to be the candidate version, and no project to reference SharedKernel sources.
    /// </summary>
    /// <exception cref="ArgumentException">A package, project, lock file entry, or local tool does not use the candidate version, a project is missing or outside the repository, or the consumer references no SharedKernel package.</exception>
    internal static void ValidateConsumer(string clone, string solution, string version)
    {
        var references = 0;
        var central = Path.Combine(clone, "Directory.Packages.props");
        if (File.Exists(central))
        {
            foreach (var package in XDocument.Load(central).Descendants("PackageVersion").Where(static package => IsCandidatePackage((string?)package.Attribute("Include"))))
            {
                var declared = (string?)package.Attribute("Version");
                if (!SameVersion(declared, version) && !SameVersion(declared, "[" + version + "]"))
                {
                    throw new ArgumentException($"Directory.Packages.props: {(string?)package.Attribute("Include")} has version {declared}, not the candidate version {version}.");
                }

                references++;
            }
        }

        var solutionDirectory = Path.GetDirectoryName(solution)!;
        foreach (var entry in XDocument.Load(solution).Descendants("Project"))
        {
            var project = Path.GetFullPath(((string?)entry.Attribute("Path") ?? throw new ArgumentException(Relative(clone, solution) + " lists a project without a path."))
                .Replace('\\', '/'), solutionDirectory);
            var name = Relative(clone, project);
            if (!RepositoryPath.IsRelative(name))
            {
                throw new ArgumentException(Relative(clone, solution) + " lists a project outside the repository: " + name + ".");
            }

            foreach (var reference in XDocument.Load(project).Descendants("ProjectReference"))
            {
                RejectSource(Path.GetFileNameWithoutExtension(((string?)reference.Attribute("Include") ?? string.Empty).Replace('\\', '/')), name);
            }

            var lockFile = Path.Combine(Path.GetDirectoryName(project)!, "packages.lock.json");
            var lockName = Relative(clone, lockFile);
            var frameworks = JsonNode.Parse(File.ReadAllText(lockFile))?["dependencies"]?.AsObject()
                ?? throw new ArgumentException(lockName + " has no dependencies.");
            foreach (var (framework, dependencies) in frameworks)
            {
                foreach (var (id, value) in (dependencies?.AsObject() ?? throw new ArgumentException(lockName + " has no dependencies for " + framework + "."))
                    .Where(static dependency => IsCandidatePackage(dependency.Key)))
                {
                    var dependency = value?.AsObject() ?? throw new ArgumentException(lockName + ": " + id + " has no entry.");
                    var type = dependency["type"]?.GetValue<string>() ?? throw new ArgumentException(lockName + ": " + id + " has no type.");
                    if (string.Equals(type, "Project", StringComparison.OrdinalIgnoreCase))
                    {
                        RejectSource(id, lockName);
                    }

                    var resolved = dependency["resolved"]?.GetValue<string>() ?? throw new ArgumentException(lockName + ": " + id + " has no resolved version.");
                    if (!SameVersion(resolved, version))
                    {
                        throw new ArgumentException($"{lockName}: {id} resolves to {resolved}, not the candidate version {version}.");
                    }

                    references++;
                }
            }
        }

        var tools = Path.Combine(clone, ".config", "dotnet-tools.json");
        if (File.Exists(tools))
        {
            var manifest = JsonNode.Parse(File.ReadAllText(tools))?["tools"]?.AsObject() ?? throw new ArgumentException(".config/dotnet-tools.json has no tools.");
            foreach (var (id, tool) in manifest.Where(static tool => IsCandidatePackage(tool.Key)))
            {
                var declared = tool?["version"]?.GetValue<string>();
                if (!SameVersion(declared, version))
                {
                    throw new ArgumentException($".config/dotnet-tools.json: {id} has version {declared}, not the candidate version {version}.");
                }

                references++;
            }
        }

        if (references == 0)
        {
            throw new ArgumentException("The consumer references no " + PackagePattern + " package.");
        }
    }

    /// <summary>
    /// Copies the candidate's packages into the folder that the clone's <c>NuGet.Config</c> maps
    /// <c>PandaBear.SharedKernel.*</c> to, and verifies the copies.
    /// </summary>
    /// <exception cref="ArgumentException">The <c>NuGet.Config</c> is missing, ambiguous, or does not map the packages to one local folder, the folder already holds packages, or a candidate package changed while staged.</exception>
    internal static async Task StagePackages(string clone, string candidate, JsonObject hashes, CancellationToken ct)
    {
        var configurations = Directory.GetFiles(clone).Where(static file => string.Equals(Path.GetFileName(file), "NuGet.Config", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (configurations.Length != 1)
        {
            throw new ArgumentException($"The repository root has {configurations.Length} NuGet.Config files; it needs one.");
        }

        var configuration = XDocument.Load(configurations[0]).Root;
        var sources = configuration?.Element("packageSources");
        if (sources?.Element("clear") is null || configuration?.Element("fallbackPackageFolders")?.Element("clear") is null)
        {
            throw new ArgumentException("NuGet.Config must clear packageSources and fallbackPackageFolders, so restore uses only the sources it lists.");
        }

        var mappings = configuration.Element("packageSourceMapping")?.Elements("packageSource").ToArray() ?? [];
        var mapped = mappings.Where(static source => Patterns(source).Any(static pattern => string.Equals(pattern, PackagePattern, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (mapped.Length != 1 || mappings.Except(mapped).Any(static source => Patterns(source).Any(static pattern => pattern.StartsWith(PackagePrefix, StringComparison.OrdinalIgnoreCase))))
        {
            throw new ArgumentException("NuGet.Config must map " + PackagePattern + " to exactly one package source.");
        }

        var key = (string?)mapped[0].Attribute("key");
        var added = sources.Elements("add").Where(source => string.Equals((string?)source.Attribute("key"), key, StringComparison.OrdinalIgnoreCase)).ToArray();
        var folder = added.Length == 1 ? (string?)added[0].Attribute("value") ?? string.Empty : string.Empty;
        if (!RepositoryPath.IsRelative(folder))
        {
            throw new ArgumentException($"NuGet.Config must add the source {key} of {PackagePattern} once, as a folder inside the repository with '/' separators and without empty, '.', or '..' segments.");
        }

        var target = RepositoryPath.Resolve(clone, folder);
        if (Directory.Exists(target) && Directory.EnumerateFiles(target, "*.nupkg", SearchOption.AllDirectories).Any())
        {
            throw new ArgumentException($"The committed package folder {folder} already holds packages, which restore could use instead of the candidate's.");
        }

        Directory.CreateDirectory(target);
        foreach (var (name, hash) in hashes)
        {
            var copy = Path.Combine(target, name);
            File.Copy(Path.Combine(Path.GetDirectoryName(candidate)!, name), copy);
            if (!string.Equals(await CandidateManifest.HashPackage(copy, ct).ConfigureAwait(false), hash?.GetValue<string>(), StringComparison.Ordinal))
            {
                throw new ArgumentException($"Candidate package changed while it was staged: {name}.");
            }
        }
    }

    private static IEnumerable<string> Patterns(XElement source) =>
        source.Elements("package").Select(static package => (string?)package.Attribute("pattern")).OfType<string>();

    private static bool IsCandidatePackage(string? id) => id?.StartsWith(PackagePrefix, StringComparison.OrdinalIgnoreCase) == true;

    private static bool SameVersion(string? declared, string version) => string.Equals(declared, version, StringComparison.OrdinalIgnoreCase);

    private static void RejectSource(string id, string path)
    {
        if (id.StartsWith("SharedKernel.", StringComparison.OrdinalIgnoreCase) || IsCandidatePackage(id))
        {
            throw new ArgumentException(path + " references SharedKernel source " + id + " instead of the candidate's package.");
        }
    }

    private static string Relative(string clone, string path) => Path.GetRelativePath(clone, path).Replace('\\', '/');

    // Git writes read-only object files, which Directory.Delete cannot remove on Windows. Links are not followed.
    private static void DeleteScratch(string scratch, TextWriter error)
    {
        if (!Directory.Exists(scratch))
        {
            return;
        }

        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
            foreach (var file in Directory.EnumerateFiles(scratch, "*", options))
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                }
            }

            Directory.Delete(scratch, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine("Warning: the scratch directory " + scratch + " was not deleted: " + exception.Message);
        }
    }
}
