namespace PandaBear.Versioning.Tool;

/// <summary>Arguments of a consumer compatibility check.</summary>
/// <param name="RepoRoot">Consumer repository root.</param>
/// <param name="Candidate">Candidate manifest to verify the consumer against.</param>
/// <param name="Solution">Solution to build, or null for the default.</param>
/// <param name="Output">File that receives the evidence; must not be the candidate manifest.</param>
/// <param name="Command">Command and arguments run against the staged candidate packages.</param>
internal sealed record ConsumerVerificationOptions(
    string RepoRoot,
    string Candidate,
    string? Solution,
    string Output,
    string[] Command);
