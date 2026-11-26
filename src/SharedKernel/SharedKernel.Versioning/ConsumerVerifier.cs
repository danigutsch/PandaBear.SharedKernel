using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using SharedKernel.ConfigurationFiles;
using SharedKernel.IO;
using SharedKernel.Processes;
using SharedKernel.Repositories;

namespace SharedKernel.Versioning;

/// <summary>
/// Runs a consumer repository's command on a clean clone of its committed revision, with a candidate's packages staged
/// in the clone's own SharedKernel package folder, and records the result as consumer compatibility evidence.
/// </summary>
/// <param name="processRunner">Runs <c>git</c> and the consumer's command, such as <see cref="SystemProcessRunner"/>.</param>
/// <param name="executableResolver">Finds <c>git</c> and a bare command name, such as <see cref="PathExecutableResolver.Default"/>.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="processRunner"/> or <paramref name="executableResolver"/> is <see langword="null"/>.</exception>
public sealed class ConsumerVerifier(IProcessRunner processRunner, IExecutableResolver executableResolver)
{
    /// <summary>The default evidence path, relative to the repository root.</summary>
    public const string DefaultOutput = "TestResults/consumer-compatibility.json";

    private const string PackagePrefix = "PandaBear.SharedKernel.";

    private const string PackagePattern = PackagePrefix + "*";

    private const int MaxReasonLength = 4096;

    private readonly ExternalCommands _commands = new(processRunner, executableResolver);

    /// <summary>
    /// Verifies the candidate and the consumer, runs the command, and writes the evidence with <c>passed</c>,
    /// <c>failed</c>, or <c>pending</c>. A canceled run writes no evidence. The scratch directory is always deleted.
    /// </summary>
    /// <param name="options">The consumer, candidate, solution, evidence file, and command.</param>
    /// <param name="output">Receives the result line.</param>
    /// <param name="error">Receives the output of the command and a scratch directory that could not be deleted.</param>
    /// <param name="ct">Cancels the run and ends the running process tree.</param>
    /// <returns>A task that completes when the evidence is <c>passed</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The evidence file would replace the candidate manifest or the execution choices are missing or conflicting.</exception>
    /// <exception cref="InvalidOperationException">The evidence is not <c>passed</c>; the message gives the reason.</exception>
    public async Task Verify(ConsumerVerificationOptions options, TextWriter output, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(options.RecipeInputs);
        if (options.Command.Count == 0 && options.Recipe is null)
        {
            throw new ArgumentException("The consumer verification needs a command to run.", nameof(options));
        }

        if (options.Command.Count != 0 && options.Recipe is not null)
        {
            throw new ArgumentException("The consumer verification accepts a command or a recipe.", nameof(options));
        }

        if (options.Recipe is null && options.RecipeInputs.Count != 0)
        {
            throw new ArgumentException("Consumer verification inputs require a recipe.", nameof(options));
        }

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
                revision = (await _commands.Run("git", ["rev-parse", "HEAD"], options.RepoRoot, ct).ConfigureAwait(false)).Trim();
                dirty = (await _commands.Run("git", ["status", "--porcelain"], options.RepoRoot, ct).ConfigureAwait(false)).Length != 0;
                evidence = CandidateManifest.Load(options.Candidate).Evidence;
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
                await _commands.Run("git", ["clone", "--quiet", "--no-checkout", "--shared", "--", options.RepoRoot, clone], scratch, ct).ConfigureAwait(false);
                await _commands.Run("git", ["checkout", "--quiet", "--detach", revision], clone, ct).ConfigureAwait(false);
                var exemptProjects = await ReadExemptProjects(clone, ct).ConfigureAwait(false);
                ValidateConsumer(clone, Solution(clone, options), version, exemptProjects);
                await StagePackages(clone, options.Candidate, evidence["packageSha256"]!.AsObject(), ct).ConfigureAwait(false);
                var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["DOTNET_CLI_HOME"] = Path.Combine(scratch, "dotnet-home"),
                    ["NUGET_PACKAGES"] = Path.Combine(scratch, "packages"),
                    ["NUGET_HTTP_CACHE_PATH"] = Path.Combine(scratch, "http-cache"),
                    ["NUGET_FALLBACK_PACKAGES"] = null,
                };
                if (options.Recipe is { } recipe)
                {
                    var steps = await ConsumerVerificationRecipe.Read(options.RepoRoot, clone, recipe, options.RecipeInputs, ct).ConfigureAwait(false);
                    await RunRecipe(steps, clone, environment, error, ct).ConfigureAwait(false);
                }
                else
                {
                    await _commands.RunStreaming(options.Command[0], [.. options.Command.Skip(1)], clone, environment, error, ct).ConfigureAwait(false);
                }

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

            new CandidateManifest(evidence).Save(options.Output);
            await output.WriteLineAsync(("Consumer compatibility: " + status + ". Evidence: " + options.Output).AsMemory(), CancellationToken.None).ConfigureAwait(false);
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

    private async Task RunRecipe(
        IReadOnlyList<(string[] Arguments, bool HasInputs)> steps, string clone,
        IReadOnlyDictionary<string, string?> environment, TextWriter error, CancellationToken ct)
    {
        for (var index = 0; index < steps.Count; index++)
        {
            var (arguments, hasInputs) = steps[index];
            try
            {
                await _commands.RunStreaming(arguments[0], arguments[1..], clone, environment, error, ct, includeArgumentsInFailure: !hasInputs)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException exception) when (!ct.IsCancellationRequested)
            {
                throw new InvalidOperationException($"Recipe step {index + 1} failed: {exception.Message}", exception);
            }
        }

        ct.ThrowIfCancellationRequested();
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
        return RepositoryPath.IsValid(relative) && relative.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
            ? RepositoryPath.Parse(relative).ResolveUnder(clone)
            : throw new ArgumentException("--solution must name a .slnx file inside the repository: " + options.Solution + ".");
    }

    private static void ValidateConsumer(string clone, string solution, string version, HashSet<string> exemptProjects)
    {
        var references = 0;
        var central = Path.Combine(clone, "Directory.Packages.props");
        if (File.Exists(central))
        {
            foreach (var package in XDocument.Load(central).Descendants("PackageVersion").Where(static package => IsCandidatePackage((string?)package.Attribute("Include"))))
            {
                var declared = (string?)package.Attribute("Version");
                if (!MatchesDeclaredVersion(declared, version))
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
            if (!RepositoryPath.IsValid(name))
            {
                throw new ArgumentException(Relative(clone, solution) + " lists a project outside the repository: " + name + ".");
            }

            var document = XDocument.Load(project);
            foreach (var reference in document.Descendants("ProjectReference"))
            {
                RejectSource(Path.GetFileNameWithoutExtension(((string?)reference.Attribute("Include") ?? string.Empty).Replace('\\', '/')), name);
            }

            var lockFile = Path.Combine(Path.GetDirectoryName(project)!, "packages.lock.json");
            var lockName = Relative(clone, lockFile);
            if (exemptProjects.Contains(name))
            {
                ValidateExemption(document, name, lockFile, version);
                continue;
            }

            references += CountLockedCandidatePackages(lockFile, lockName, version);
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

    private static async Task<HashSet<string>> ReadExemptProjects(string clone, CancellationToken ct)
    {
        var config = ConsumerConfigurationFile.RelativePath;
        var path = RepositoryPath.Parse(config).ResolveUnder(clone);
        try
        {
            var content = await BoundedReader.ReadFile(path, JsonConfigurationFile.MaxBytes, ct).ConfigureAwait(false)
                ?? throw new ArgumentException(config + " exceeds the 1 MiB configuration size limit.");
            return ConsumerConfigurationFile.ParseLockExemptProjects(content).ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static void ValidateExemption(XDocument project, string name, string lockFile, string version)
    {
        var declarations = project.Root?.Elements("PropertyGroup")
            .SelectMany(static group => group.Elements("RestorePackagesWithLockFile")).ToArray();
        if (declarations is not [var setting]
            || setting.Parent?.Attribute("Condition") is not null
            || setting.Attribute("Condition") is not null
            || !string.Equals(setting.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(name + " is listed in lockExemptProjects and must set RestorePackagesWithLockFile to false unconditionally in its project file.");
        }

        if (File.Exists(lockFile))
        {
            throw new ArgumentException(name + " is listed in lockExemptProjects and must not keep a packages.lock.json.");
        }

        ValidateExemptPackageDeclarations(project, name, version);
    }

    private static void ValidateExemptPackageDeclarations(XDocument project, string name, string version)
    {
        foreach (var package in project.Descendants("PackageReference"))
        {
            var id = (string?)package.Attribute("Include") ?? (string?)package.Attribute("Update");
            if (!IsCandidatePackage(id))
            {
                continue;
            }

            var versions = package.Attributes().Where(static attribute => attribute.Name.LocalName is "Version" or "VersionOverride")
                .Select(static attribute => attribute.Value)
                .Concat(package.Elements().Where(static element => element.Name.LocalName is "Version" or "VersionOverride")
                    .Select(static element => element.Value));
            foreach (var declared in versions)
            {
                if (!MatchesDeclaredVersion(declared, version))
                {
                    throw new ArgumentException($"{name}: {id} has version {declared}, not the candidate version {version}.");
                }
            }
        }
    }

    private static int CountLockedCandidatePackages(string lockFile, string lockName, string version)
    {
        if (!File.Exists(lockFile))
        {
            throw new ArgumentException(lockName + " is required unless the project is declared in lockExemptProjects.");
        }

        var frameworks = JsonNode.Parse(File.ReadAllText(lockFile))?["dependencies"]?.AsObject()
            ?? throw new ArgumentException(lockName + " has no dependencies.");
        var references = 0;
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

        return references;
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
        if (!RepositoryPath.IsValid(folder))
        {
            throw new ArgumentException($"NuGet.Config must add the source {key} of {PackagePattern} once, as a folder inside the repository with '/' separators and without empty, '.', or '..' segments.");
        }

        var target = RepositoryPath.Parse(folder).ResolveUnder(clone);
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

    private static bool MatchesDeclaredVersion(string? declared, string version) =>
        SameVersion(declared, version) || SameVersion(declared, "[" + version + "]");

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
