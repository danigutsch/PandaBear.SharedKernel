namespace PandaBear.RepoConfig.Tests;

/// <summary>An archive entry: a regular file with <paramref name="Content"/>, or a symbolic link to <paramref name="LinkTarget"/>.</summary>
/// <param name="Name">The path of the entry inside the archive.</param>
/// <param name="Content">The file content, or <see langword="null"/> for an empty file or a link.</param>
/// <param name="LinkTarget">The link target, or <see langword="null"/> for a regular file.</param>
internal sealed record DevcontainerCliArchiveEntry(string Name, string? Content = null, string? LinkTarget = null);
