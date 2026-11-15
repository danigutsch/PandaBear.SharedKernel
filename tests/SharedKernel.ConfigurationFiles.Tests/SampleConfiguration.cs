namespace PandaBear.ConfigurationFiles.Tests;

/// <summary>A configuration with a version and one required name.</summary>
/// <param name="Version">The format version.</param>
/// <param name="Name">A required name.</param>
internal sealed record SampleConfiguration(int Version, string Name);
