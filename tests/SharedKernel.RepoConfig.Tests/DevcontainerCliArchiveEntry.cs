namespace PandaBear.RepoConfig.Tests;

/// <summary>An archive entry: a regular file with <paramref name="Content"/>, or a symbolic link to <paramref name="LinkTarget"/>.</summary>
internal sealed record DevcontainerCliArchiveEntry(string Name, string? Content = null, string? LinkTarget = null);
