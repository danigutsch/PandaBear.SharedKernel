using System.Xml.Linq;

namespace PandaBear.Versioning.Tool;

internal static class PackageConsumptionCommand
{
    /// <summary>Restores, builds, and runs the samples against the packages in <paramref name="feed" />, writing child process output to <paramref name="error" />.</summary>
    public static async Task Run(string root, string feed, string version, bool aot, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var packages = Directory.GetFiles(feed, "*.nupkg");
        if (packages.Length == 0) { throw new ArgumentException("Candidate feed contains no packages."); }
        var packageIds = SharedKernelLocalFeed.ReadPackageIds(packages, version);
        var external = XDocument.Load(Path.Combine(root, "Directory.Packages.props"))
            .Descendants("PackageVersion").ToDictionary(x => (string)x.Attribute("Include")!, x => (string)x.Attribute("Version")!, StringComparer.Ordinal);
        var scratch = Path.Combine(Path.GetTempPath(), "sharedkernel-package-check-" + Guid.NewGuid().ToString("N"));
        foreach (var relative in new[] { "samples/Results/BasicResults.Sample", "samples/Mediator/Mediator.Sample", "samples/HttpRequests/HttpRequests.Sample" })
        {
            var source = Path.Combine(root, relative);
            var target = Path.Combine(scratch, Path.GetFileName(relative));
            Directory.CreateDirectory(target);
            File.Copy(Path.Combine(root, "global.json"), Path.Combine(target, "global.json"));
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var local = Path.GetRelativePath(source, file);
                if (local.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or "Generated") || Path.GetFileName(file) == "packages.lock.json")
                {
                    continue;
                }

                var destination = Path.Combine(target, local);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }

            var projectPath = Directory.GetFiles(target, "*.csproj").Single();
            var project = XDocument.Load(projectPath);
            var versions = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var reference in project.Descendants("ProjectReference").ToArray())
            {
                var id = Path.GetFileNameWithoutExtension(((string)reference.Attribute("Include")!).Replace('\\', '/'));
                id = "PandaBear." + id;
                reference.ReplaceWith(new XElement("PackageReference", new XAttribute("Include", id)));
                versions[id] = "[" + version + "]";
            }

            foreach (var reference in project.Descendants("PackageReference"))
            {
                var id = (string)reference.Attribute("Include")!;
                versions.TryAdd(id, external.GetValueOrDefault(id) ?? "[" + version + "]");
            }

            project.Root!.Add(new XElement("PropertyGroup", new XElement("TargetFramework", "net10.0"),
                new XElement("ImplicitUsings", "enable"), new XElement("Nullable", "enable"),
                new XElement("TreatWarningsAsErrors", "true"), new XElement("NuGetAudit", "true"),
                new XElement("NuGetAuditMode", "all"), new XElement("NuGetAuditLevel", "high")));
            project.Save(projectPath);
            await File.WriteAllTextAsync(Path.Combine(target, ".editorconfig"), "root = true\n", ct).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(target, "Directory.Build.props"), "<Project />\n", ct).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(target, "Directory.Build.targets"), "<Project />\n", ct).ConfigureAwait(false);
            new XDocument(new XElement("Project", new XElement("PropertyGroup", new XElement("ManagePackageVersionsCentrally", "true")),
                new XElement("ItemGroup", versions.Select(pair => new XElement("PackageVersion", new XAttribute("Include", pair.Key), new XAttribute("Version", pair.Value))))))
                .Save(Path.Combine(target, "Directory.Packages.props"));
            await SharedKernelLocalFeed.WriteNuGetConfig(target, feed, packageIds, new Uri(string.Concat("https://api.nuget.org", "/v3/index.json"))).ConfigureAwait(false);
            var config = Path.Combine(target, "NuGet.config");
            var cache = Path.Combine(scratch, "packages-" + Path.GetFileName(relative));
            string[][] commands = [
                ["restore", projectPath, "--configfile", config, "--packages", cache, "--no-cache", "--use-lock-file"],
                ["restore", projectPath, "--configfile", config, "--packages", cache, "--locked-mode"],
                ["build", projectPath, "-c", "Release", "--no-restore"],
                ["run", "--project", projectPath, "-c", "Release", "--no-build", "--no-restore"]
            ];
            foreach (var command in commands)
            {
                await error.WriteAsync(await CommandRunner.Run("dotnet", command, target, ct).ConfigureAwait(false)).ConfigureAwait(false);
            }

            if (aot)
            {
                var rid = OperatingSystem.IsWindows() ? "win-x64" : "linux-x64";
                var native = Path.Combine(target, "native");
                await error.WriteAsync(await CommandRunner.Run("dotnet", ["publish", projectPath, "-c", "Release", "-r", rid,
                    "-p:PublishAot=true", "--configfile", config, "--packages", cache, "-o", native], target, ct).ConfigureAwait(false)).ConfigureAwait(false);
                await error.WriteAsync(await CommandRunner.Run(
                    Path.Combine(native, Path.GetFileName(relative) + (OperatingSystem.IsWindows() ? ".exe" : string.Empty)), [], target, ct).ConfigureAwait(false)).ConfigureAwait(false);
            }
        }

        await output.WriteLineAsync("Package consumption checks passed. Evidence workspace: " + scratch).ConfigureAwait(false);
    }
}
