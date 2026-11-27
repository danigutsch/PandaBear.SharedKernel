using System.Security.Cryptography;
using SharedKernel.Testing.Packaging;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Seeds an external cache with a different synthetic archive for the trial's candidate.</summary>
internal static class NuGetTrialCallerCache
{
    /// <summary>The parent test sets this only for the explicit child probe.</summary>
    internal const string ProbeVariable = "SHAREDKERNEL_NUGET_TRIAL_CACHE_PROBE";

    /// <summary>Restores the external archive without its fixture counterpart's dependency.</summary>
    internal static async Task<string> Seed(string directory, CancellationToken ct)
    {
        var feed = Path.Combine(directory, "feed");
        var cache = Path.Combine(directory, "caller-cache");
        _ = NuGetPackageBuilder.WritePackage(feed, NuGetTrialRepository.Driver, "2.0.0");
        await File.WriteAllTextAsync(Path.Combine(directory, "global.json"),
            """{"sdk":{"version":"11.0.100","rollForward":"disable"}}""", ct);
        await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"), """
            <configuration>
              <packageSources><clear/><add key="caller" value="feed"/></packageSources>
            </configuration>
            """, ct);
        await File.WriteAllTextAsync(Path.Combine(directory, "Caller.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="{{NuGetTrialRepository.Driver}}" Version="2.0.0"/></ItemGroup>
            </Project>
            """, ct);
        var seeded = await TestProcess.Run("dotnet", directory,
            ["restore", "Caller.csproj", "--packages", cache, "--configfile", "NuGet.Config", "--disable-build-servers",
                "-p:NuGetAudit=false"], ct);
        seeded.ExitCode.ShouldBe(0, seeded.StandardOutput + seeded.StandardError);
        return cache;
    }

    /// <summary>Captures the caller-owned files and their exact content.</summary>
    internal static string[] Inventory(string cache) => Directory.EnumerateFiles(cache, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal)
        .Select(path => Describe(cache, path))
        .ToArray();

    private static string Describe(string cache, string path)
    {
        using var stream = File.OpenRead(path);
        return Path.GetRelativePath(cache, path) + ":" + Convert.ToHexString(SHA256.HashData(stream));
    }
}
