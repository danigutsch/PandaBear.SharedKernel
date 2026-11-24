namespace PandaBear.Repositories;

/// <summary>The SDK-evaluated direct dependencies of a tracked standalone C# application.</summary>
/// <param name="Path">The application source path relative to its checkout.</param>
/// <param name="VirtualProjectPath">The evaluated MSBuild project path relative to the checkout.</param>
/// <param name="Framework">The evaluated target framework.</param>
/// <param name="Packages">The requested version and central declaration for each direct package.</param>
/// <param name="HasProjectReferences">Whether the app also references MSBuild projects.</param>
/// <param name="LockFile">The repository-relative SDK lock location when the app uses a package lock.</param>
internal sealed record NuGetFileApp(
    string Path,
    string VirtualProjectPath,
    string Framework,
    IReadOnlyDictionary<string, (string? Version, string? CentralFile)> Packages,
    bool HasProjectReferences,
    string? LockFile);
