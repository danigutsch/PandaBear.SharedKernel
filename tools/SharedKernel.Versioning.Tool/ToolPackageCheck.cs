using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace PandaBear.Versioning.Tool;

/// <summary>Installs the tool packages of a package feed and runs each of their commands with <c>--help</c>.</summary>
internal static class ToolPackageCheck
{
    private const string SettingsFileName = "DotnetToolSettings.xml";

    /// <summary>
    /// Installs every tool package in <paramref name="packages" /> into a scratch tool path, from a NuGet configuration whose
    /// only source is <paramref name="feed" />, and runs each command that the package declares with <c>--help</c> from an
    /// empty scratch directory. Child process output goes to <paramref name="error" />. The scratch directory is deleted
    /// afterwards.
    /// </summary>
    /// <exception cref="ArgumentException">A tool package declares no command or an invalid one.</exception>
    /// <exception cref="InvalidOperationException">A tool package does not install, or a command exits non-zero.</exception>
    public static async Task Run(
        string root,
        string feed,
        string version,
        IReadOnlyList<string> packages,
        Func<string, IEnumerable<string>, string?, CancellationToken, Task<string>> run,
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
            await WriteNuGetConfig(configuration, feed, ct).ConfigureAwait(false);
            foreach (var (id, commands) in tools)
            {
                await RunStep(
                    run,
                    "dotnet",
                    ["tool", "install", "--tool-path", toolPath, id, "--version", version, "--configfile", configuration],
                    scratch,
                    $"Tool package {id} did not install",
                    error,
                    ct).ConfigureAwait(false);
                foreach (var command in commands)
                {
                    var shim = Path.Combine(toolPath, OperatingSystem.IsWindows() ? command + ".exe" : command);
                    await RunStep(run, shim, ["--help"], workingDirectory, $"Tool command {command} of package {id} failed with --help", error, ct)
                        .ConfigureAwait(false);
                }
            }
        }
        finally
        {
            await Delete(scratch, error).ConfigureAwait(false);
        }
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
                if (name.Length == 0 || name.StartsWith('.') || name.IndexOfAny(['/', '\\']) >= 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
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

    /// <summary>Runs a process, writes its output to <paramref name="error" />, and prefixes its failure with <paramref name="failure" />.</summary>
    private static async Task RunStep(
        Func<string, IEnumerable<string>, string?, CancellationToken, Task<string>> run,
        string fileName,
        string[] arguments,
        string workingDirectory,
        string failure,
        TextWriter error,
        CancellationToken ct)
    {
        try
        {
            await error.WriteAsync(await run(fileName, arguments, workingDirectory, ct).ConfigureAwait(false)).ConfigureAwait(false);
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
