namespace PandaBear.Versioning.Tests;

/// <summary>Fake "ask dotnet" steps for global packages folder resolution.</summary>
internal static class GlobalPackagesFakes
{
    /// <summary>Creates a query that reports the given folder the way <c>dotnet nuget locals global-packages --list</c> would.</summary>
    /// <param name="folder">The reported folder.</param>
    /// <returns>The fake query.</returns>
    internal static Func<CancellationToken, Task<string?>> Reporting(string folder) =>
        _ => Task.FromResult<string?>(folder);
}
