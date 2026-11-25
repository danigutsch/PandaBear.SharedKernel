namespace PandaBear.Repositories;

/// <summary>The availability and content of one evaluated project graph.</summary>
/// <param name="LockExempt">Whether evaluated project settings explicitly disable package lock files.</param>
/// <param name="Graph">The current restored package graph, if available.</param>
/// <param name="Unavailable">The reason a current restored graph could not be read.</param>
internal sealed record NuGetRestoreProject(bool LockExempt, NuGetRestoreGraph? Graph, string? Unavailable)
{
    /// <summary>The effective SDK settings for audit candidate discovery, when present.</summary>
    internal NuGetVersionSources? Sources { get; init; }
}
