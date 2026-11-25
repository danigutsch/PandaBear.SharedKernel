namespace PandaBear.Repositories;

/// <summary>The configuration files and source locations from an evaluated restore input.</summary>
/// <param name="ConfigFiles">The configuration files, in SDK precedence order.</param>
/// <param name="Locations">The effective package source paths and URLs.</param>
internal sealed record NuGetVersionSources(string[] ConfigFiles, string[] Locations);
