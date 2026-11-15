namespace PandaBear.Packages.Tests;

/// <summary>Runs a package tool the way the readers do: from the start information they build, through the bounded runner.</summary>
internal static class PackageToolProcess
{
    /// <summary>Runs <paramref name="executable"/> with <paramref name="arguments"/> and returns its output.</summary>
    public static Task<ProcessOutput> Run(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        new BoundedProcessRunner().Run(PackageReaderSupport.CreateStartInfo(executable, arguments), null, cancellationToken);
}
