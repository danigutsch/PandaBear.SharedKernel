using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace PandaBear.Versioning;

/// <summary>
/// Audits the packages that the tool packages of a package feed carry, installs the tools, and runs each of their commands
/// with <c>--help</c>.
/// </summary>
internal static class ToolPackageCheck
{
    private const string SettingsFileName = "DotnetToolSettings.xml";
    private const string ManifestSuffix = ".deps.json";

    /// <summary>
    /// Audits the packages that every tool package in <paramref name="packages" /> carries, as <see cref="Audit" /> describes,
    /// then installs each tool into a scratch tool path, from a NuGet configuration whose only source is <paramref name="feed" />,
    /// and runs each command that the package declares with <c>--help</c> from an empty scratch directory. Child process output
    /// goes to <paramref name="error" />. The scratch directory is deleted afterwards.
    /// </summary>
    /// <exception cref="ArgumentException">A tool package declares no command or an invalid one, or lacks a valid dependency manifest.</exception>
    /// <exception cref="InvalidOperationException">The audit of a tool package fails, a tool package does not install, or a command exits non-zero.</exception>
    public static async Task Run(
        string root,
        string feed,
        string version,
        IReadOnlyList<string> packages,
        Uri nuGetSource,
        ExternalCommands processes,
        TextWriter error,
        CancellationToken ct)
    {
        var tools = packages.Where(IsToolPackage)
            .Select(package => (Id: SharedKernelLocalFeed.ReadPackageIds([package], version).Single(), Commands: ReadCommandNames(package)))
            .OrderBy(static tool => tool.Id, StringComparer.Ordinal)
            .ToArray();
        if (tools.Length == 0)
        {
            return;
        }

        var scratch = Path.Combine(Path.GetTempPath(), "sharedkernel-tool-check-" + Guid.NewGuid().ToString("N"));
        var toolPath = Path.Combine(scratch, "tools");
        var workingDirectory = Path.Combine(scratch, "work");
        var configuration = Path.Combine(scratch, "NuGet.config");
        try
        {
            Directory.CreateDirectory(workingDirectory);

            // The SDK pinned in global.json installs the tools, as it builds the samples.
            File.Copy(Path.Combine(root, "global.json"), Path.Combine(scratch, "global.json"));
            await Audit(Path.Combine(scratch, "audit"), feed, version, packages, nuGetSource, processes, error, ct).ConfigureAwait(false);
            await WriteNuGetConfig(configuration, feed, ct).ConfigureAwait(false);
            foreach (var (id, commands) in tools)
            {
                await RunStep(
                    processes,
                    "dotnet",
                    ["tool", "install", "--tool-path", toolPath, id, "--version", version, "--configfile", configuration],
                    scratch,
                    $"Tool package {id} did not install",
                    error,
                    ct).ConfigureAwait(false);
                foreach (var command in commands)
                {
                    var shim = Path.Combine(toolPath, OperatingSystem.IsWindows() ? command + ".exe" : command);
                    await RunStep(processes, shim, ["--help"], workingDirectory, $"Tool command {command} of package {id} failed with --help", error, ct)
                        .ConfigureAwait(false);
                }
            }
        }
        finally
        {
            await Delete(scratch, error).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Audits with NuGet Audit the packages that each tool package in <paramref name="packages" /> carries, because
    /// <c>dotnet tool install</c> restores no project and so audits nothing, and NuGet cannot reference a tool package from a
    /// project. For every framework-dependent target framework of a tool that carries packages, it restores a project in
    /// <paramref name="directory" /> that references each package of the tool's dependency manifest at its exact version, with
    /// the audit settings of a consumer project (<see cref="PackageConsumption.AddConsumerProperties" />): warnings as errors and
    /// every direct and transitive package audited at NuGet's default level. The NuGet configuration maps the SharedKernel
    /// packages to <paramref name="feed" /> and takes the other packages and the vulnerability data from
    /// <paramref name="nuGetSource" />, its only audit source, so an advisory of any severity (NU1901 to NU1904) and an audit
    /// that gets no vulnerability data (NU1900, NU1905) fail the restore. Restore output goes to <paramref name="error" />.
    /// </summary>
    /// <exception cref="ArgumentException">A tool package lacks a valid dependency manifest, or a package does not have <paramref name="version" />.</exception>
    /// <exception cref="InvalidOperationException">A restore fails; the message names the tool package and the target framework.</exception>
    public static async Task Audit(
        string directory,
        string feed,
        string version,
        IReadOnlyList<string> packages,
        Uri nuGetSource,
        ExternalCommands processes,
        TextWriter error,
        CancellationToken ct)
    {
        var audits = packages.Where(IsToolPackage)
            .Select(package => (Id: SharedKernelLocalFeed.ReadPackageIds([package], version).Single(), Frameworks: ReadCarriedPackages(package)))
            .OrderBy(static tool => tool.Id, StringComparer.Ordinal)
            .SelectMany(static tool => tool.Frameworks
                .Where(static framework => framework.Packages.Length > 0)
                .Select(framework => (tool.Id, framework.Framework, framework.Packages)))
            .ToArray();
        if (audits.Length == 0)
        {
            return;
        }

        Directory.CreateDirectory(directory);
        foreach (var name in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props" })
        {
            await File.WriteAllTextAsync(Path.Combine(directory, name), "<Project />" + Environment.NewLine, ct).ConfigureAwait(false);
        }

        await SharedKernelLocalFeed.WriteNuGetConfig(directory, feed, SharedKernelLocalFeed.ReadPackageIds([.. packages], version), nuGetSource).ConfigureAwait(false);
        for (var index = 0; index < audits.Length; index++)
        {
            var (id, framework, carried) = audits[index];
            var projectDirectory = Path.Combine(directory, index.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(projectDirectory);
            var project = new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("ItemGroup", carried.Select(static package =>
                    new XElement("PackageReference", new XAttribute("Include", package.Id), new XAttribute("Version", "[" + package.Version + "]"))))));
            PackageConsumption.AddConsumerProperties(project, framework);
            var projectPath = Path.Combine(projectDirectory, "ToolAudit.csproj");
            project.Save(projectPath);
            await RunStep(
                processes,
                "dotnet",
                ["restore", projectPath, "--configfile", Path.Combine(directory, "NuGet.config"), "--packages", Path.Combine(directory, "packages"), "--no-http-cache"],
                projectDirectory,
                $"Tool package {id} failed the NuGet audit of the packages it carries for {framework}",
                error,
                ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the NuGet packages that a tool package carries for each framework-dependent target framework: the libraries of type
    /// <c>package</c>, named <c>Id/Version</c>, in the dependency manifests (<c>tools/&lt;tfm&gt;/any/*.deps.json</c>) beside each
    /// <c>tools/&lt;tfm&gt;/any/DotnetToolSettings.xml</c>. The build writes these manifests, and the tool's own projects are not
    /// packages.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A framework folder has no dependency manifest, or a manifest is not valid JSON or does not list its libraries as objects
    /// named by id and version.
    /// </exception>
    public static (string Framework, (string Id, string Version)[] Packages)[] ReadCarriedPackages(string package)
    {
        using var archive = ZipFile.OpenRead(package);
        var entries = archive.Entries
            .Select(static entry => (Entry: entry, Segments: entry.FullName.Split('/')))
            .Where(static entry => entry.Segments is ["tools", _, "any", _])
            .ToArray();
        return [.. entries.Where(static entry => entry.Segments[3] == SettingsFileName)
            .Select(static entry => entry.Segments[1])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(framework =>
            {
                var manifests = entries
                    .Where(entry => entry.Segments[1] == framework && entry.Segments[3].EndsWith(ManifestSuffix, StringComparison.Ordinal))
                    .ToArray();
                return manifests.Length == 0
                    ? throw new ArgumentException($"{package}: expected a dependency manifest tools/{framework}/any/*{ManifestSuffix}, which lists the packages to audit")
                    : (framework, manifests.SelectMany(manifest => ReadManifest(package, manifest.Entry))
                        .Distinct()
                        .OrderBy(static carried => carried.Id, StringComparer.Ordinal)
                        .ThenBy(static carried => carried.Version, StringComparer.Ordinal)
                        .ToArray());
            })];
    }

    /// <summary>Determines whether a package declares the <c>DotnetTool</c> package type.</summary>
    public static bool IsToolPackage(string package)
    {
        using var archive = ZipFile.OpenRead(package);
        using var stream = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open();
        return XDocument.Load(stream).Descendants().Any(element => element.Name.LocalName == "packageType"
            && (string?)element.Attribute("name") == "DotnetTool");
    }

    /// <summary>
    /// Reads the command names that the <c>tools/&lt;tfm&gt;/any/DotnetToolSettings.xml</c> files of a tool package declare,
    /// without processing document type definitions, so no entity expands and no external resource is read.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A settings file is not valid XML or names a command that is not a plain file name, or no settings file declares a command.
    /// </exception>
    public static string[] ReadCommandNames(string package)
    {
        using var archive = ZipFile.OpenRead(package);
        var commands = new List<string>();
        foreach (var entry in archive.Entries.Where(static entry => entry.FullName.Split('/') is ["tools", _, "any", SettingsFileName]))
        {
            XDocument settings;
            try
            {
                using var stream = entry.Open();
                using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                settings = XDocument.Load(reader);
            }
            catch (XmlException exception)
            {
                throw new ArgumentException($"{package}: invalid {entry.FullName}: {exception.Message}", exception);
            }

            foreach (var command in settings.Root?.Element("Commands")?.Elements("Command") ?? [])
            {
                // The name becomes the file name of a shim in the tool path, so it must not name another path.
                var name = (string?)command.Attribute("Name") ?? string.Empty;
                if (name.Length == 0 || name.StartsWith('.', StringComparison.Ordinal) || name.IndexOfAny(['/', '\\']) >= 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new ArgumentException($"{package}: invalid command name '{name}' in {entry.FullName}");
                }

                commands.Add(name);
            }
        }

        return commands.Count == 0
            ? throw new ArgumentException($"{package}: expected a command in tools/<tfm>/any/{SettingsFileName}")
            : [.. commands.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    /// <summary>Reads the package libraries of one dependency manifest.</summary>
    /// <exception cref="ArgumentException">The manifest is not valid JSON or does not list its libraries as objects named by id and version.</exception>
    private static List<(string Id, string Version)> ReadManifest(string package, ZipArchiveEntry entry)
    {
        JsonDocument document;
        try
        {
            using var stream = entry.Open();
            document = JsonDocument.Parse(stream);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException($"{package}: invalid {entry.FullName}: {exception.Message}", exception);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("libraries", out var libraries)
                || libraries.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException($"{package}: invalid {entry.FullName}: expected an object 'libraries'");
            }

            var carried = new List<(string Id, string Version)>();
            foreach (var library in libraries.EnumerateObject())
            {
                if (library.Value.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException($"{package}: invalid {entry.FullName}: library '{library.Name}' is not an object");
                }

                if (!library.Value.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || !type.ValueEquals("package"))
                {
                    continue;
                }

                var separator = library.Name.IndexOf('/', StringComparison.Ordinal);
                if (separator <= 0 || separator == library.Name.Length - 1)
                {
                    throw new ArgumentException($"{package}: invalid {entry.FullName}: package library '{library.Name}' is not named Id/Version");
                }

                carried.Add((library.Name[..separator], library.Name[(separator + 1)..]));
            }

            return carried;
        }
    }

    /// <summary>Runs a process, writes its output to <paramref name="error" />, and prefixes its failure with <paramref name="failure" />.</summary>
    private static async Task RunStep(
        ExternalCommands processes,
        string fileName,
        string[] arguments,
        string workingDirectory,
        string failure,
        TextWriter error,
        CancellationToken ct)
    {
        try
        {
            await error.WriteAsync((await processes.Run(fileName, arguments, workingDirectory, ct).ConfigureAwait(false)).AsMemory(), CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(failure + ": " + exception.Message, exception);
        }
    }

    private static Task WriteNuGetConfig(string path, string feed, CancellationToken ct)
    {
        var configuration = new XElement("configuration",
            new XElement("packageSources",
                new XElement("clear"),
                new XElement("add", new XAttribute("key", "sharedkernel-candidate"), new XAttribute("value", Path.GetFullPath(feed)))));
        return File.WriteAllTextAsync(path, configuration + Environment.NewLine, ct);
    }

    private static async Task Delete(string scratch, TextWriter error)
    {
        try
        {
            if (Directory.Exists(scratch))
            {
                Directory.Delete(scratch, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A scratch directory left behind does not change the result of the check, so it is reported, not raised.
            await error.WriteLineAsync($"Could not delete {scratch}: {exception.Message}").ConfigureAwait(false);
        }
    }
}
