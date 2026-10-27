namespace PandaBear.Versioning.Tool;

internal sealed record ConsumerVerificationOptions(
    string RepoRoot,
    string Candidate,
    string? Solution,
    string Output,
    string[] Command);
