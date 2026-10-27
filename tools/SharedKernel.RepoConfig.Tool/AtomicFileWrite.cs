namespace PandaBear.RepoConfig.Tool;

/// <summary>A planned file replacement that applies only when the file still has the expected content.</summary>
/// <param name="Path">Path of the file to write.</param>
/// <param name="Content">New file content.</param>
/// <param name="ExpectedContent">Content the file must currently have, or <see langword="null" /> when the file must not exist.</param>
internal sealed record AtomicFileWrite(string Path, string Content, string? ExpectedContent);
