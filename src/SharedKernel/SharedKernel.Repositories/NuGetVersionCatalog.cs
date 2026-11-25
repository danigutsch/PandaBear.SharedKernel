using System.Globalization;
using System.Text.Json;
using SharedKernel.Processes;

namespace SharedKernel.Repositories;

/// <summary>Runs the repository-selected SDK's managed NuGet APIs in an isolated subprocess.</summary>
internal static class NuGetVersionCatalog
{
    private const int VersionLimit = 512;
    private const int InputBytesLimit = 2 * 1024 * 1024;
    private static readonly string[] SourceFiles =
    [
        "Resolver.cs", "Resolver.csproj", "NuGetVersionSources.cs", "NuGetVersionRequestPackage.cs", "NuGetVersionRequest.cs",
        "NuGetVersionSet.cs", "NuGetVersionResponse.cs",
    ];

    /// <summary>Returns complete and incomplete source enumeration evidence without exposing source settings.</summary>
    internal static async Task<NuGetVersionSet[]> Read(string root, string sdk, NuGetVersionRequestPackage[] packages, CancellationToken ct, IProcessRunner? runner = null)
    {
        if (!int.TryParse(sdk.Split('.', 2)[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major) || major is not (10 or 11))
        {
            return Unavailable(packages, "sdkUnsupported");
        }

        var input = JsonSerializer.SerializeToUtf8Bytes(new NuGetVersionRequest(1, packages, VersionLimit),
            NuGetRestoreGraphJsonContext.Default.NuGetVersionRequest);
        if (input.Length > InputBytesLimit)
        {
            return Unavailable(packages, "inputLimitReached");
        }

        var scratch = Directory.CreateTempSubdirectory("sharedkernel-nuget-metadata-");
        try
        {
            var target = FormattableString.Invariant($"net{major}.0");
            foreach (var name in SourceFiles)
            {
                using var resource = typeof(NuGetVersionCatalog).Assembly.GetManifestResourceStream("NuGetMetadata." + name)
                    ?? throw new InvalidOperationException("An embedded SDK metadata resolver file is missing.");
                using var reader = new StreamReader(resource);
                var content = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(scratch.FullName, name),
                    name == "Resolver.csproj" ? content.Replace("SDK_TARGET", target, StringComparison.Ordinal) : content, ct).ConfigureAwait(false);
            }

            var project = Path.Combine(scratch.FullName, "Resolver.csproj");
            var config = Path.Combine(scratch.FullName, "Offline.Config");
            var request = Path.Combine(scratch.FullName, "request.json");
            await File.WriteAllBytesAsync(request, input, ct).ConfigureAwait(false);
            await File.WriteAllTextAsync(config,
                "<configuration><packageSources><clear/></packageSources><auditSources><clear/></auditSources></configuration>", ct)
                .ConfigureAwait(false);
            runner ??= new BoundedProcessRunner();
            var environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MSBUILDDISABLENODEREUSE"] = "1",
                ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0",
                ["NUGET_HTTP_CACHE_PATH"] = Path.Combine(scratch.FullName, "http-cache"),
            };
            string[] isolatedBuild = ["--disable-build-servers", "-m:1", "-p:ImportDirectoryBuildProps=false", "-p:ImportDirectoryBuildTargets=false"];
            var restore = await NuGetRestoreGraphComparison.RunDotnet(root, runner, ["restore", project, "--configfile", config, .. isolatedBuild], ct,
                environment).ConfigureAwait(false);
            if (restore.ExitCode != 0)
            {
                return Unavailable(packages, "resolverRestoreFailed");
            }

            var build = await NuGetRestoreGraphComparison.RunDotnet(root, runner, ["build", project, "--no-restore", "-c", "Release", .. isolatedBuild], ct,
                environment).ConfigureAwait(false);
            if (build.ExitCode != 0)
            {
                return Unavailable(packages, "resolverBuildFailed");
            }

            var resolver = Path.Combine(scratch.FullName, "bin", "Release", target, "Resolver.dll");
            var executed = await NuGetRestoreGraphComparison.RunDotnet(root, runner, [resolver, request], ct, environment).ConfigureAwait(false);
            return executed.ExitCode == 0
                ? Parse(executed.StandardOutput, packages) ?? Unavailable(packages, "resolverResponseInvalid")
                : Unavailable(packages, "resolverExecutionFailed");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException or UnauthorizedAccessException)
        {
            return Unavailable(packages, "resolverUnavailable");
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    private static NuGetVersionSet[] Unavailable(NuGetVersionRequestPackage[] packages, string status) =>
        packages.Select(package => new NuGetVersionSet(package.Package, status, [])).ToArray();

    private static NuGetVersionSet[]? Parse(string json, NuGetVersionRequestPackage[] request)
    {
        try
        {
            var reply = JsonSerializer.Deserialize(json, NuGetRestoreGraphJsonContext.Default.NuGetVersionResponse);
            if (reply is not { SchemaVersion: 1, Packages: { } packages } || packages.Any(package => package is null)
                || !packages.Select(package => package.Package).SequenceEqual(request.Select(package => package.Package), StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }

            return packages.All(package => package.Status is "complete" or "mappingUnavailable" or "partial" or "feedUnavailable" or "ambiguousSources" or "limitExceeded"
                && package.Versions is { Length: <= VersionLimit } versions
                && versions.Distinct(StringComparer.OrdinalIgnoreCase).Count() == versions.Length
                && versions.All(version => version is not null && NuGetTrialProps.IsLiteralVersion(version)))
                ? packages : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
