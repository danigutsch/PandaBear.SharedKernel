namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// One approved third-party GitHub Action.
/// </summary>
/// <param name="Repository">The <c>owner/repository</c> of the Action; a path inside the repository is not part of it.</param>
/// <param name="Sha">The full 40-character lowercase commit SHA that every reference must pin.</param>
/// <param name="Version">The release that <paramref name="Sha"/> belongs to, which every reference repeats as its trailing comment.</param>
internal sealed record ActionPin(string Repository, string Sha, string Version);
