namespace PandaBear.CI.Tool;

/// <summary>The resolved options of one <c>stage-candidate</c> run.</summary>
/// <param name="Root">The consumer repository root, whose <c>global.json</c> must match the library's.</param>
/// <param name="Pin">The candidate manifest that the consumer pins.</param>
/// <param name="LibraryRoot">A clean library checkout at the pinned revision.</param>
/// <param name="Output">The local feed that receives the packages and <c>candidate.json</c>.</param>
/// <param name="Repository">The GitHub repository whose release supplies the packages, or <see langword="null"/>.</param>
/// <param name="Feed">The directory that supplies the packages, or <see langword="null"/>.</param>
internal sealed record CandidateStagingRequest(string Root, string Pin, string LibraryRoot, string Output, string? Repository, string? Feed);
