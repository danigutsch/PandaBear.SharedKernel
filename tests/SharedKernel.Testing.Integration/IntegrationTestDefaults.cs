namespace SharedKernel.Testing.Integration;

/// <summary>
/// Shared defaults for dependency-heavy integration test support.
/// </summary>
public static class IntegrationTestDefaults
{
    /// <summary>
    /// Default timeout for hosted resource startup waits.
    /// </summary>
    public static readonly TimeSpan ResourceStartupTimeout = TimeSpan.FromSeconds(90);
}
