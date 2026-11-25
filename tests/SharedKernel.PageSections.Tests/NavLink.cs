namespace SharedKernel.PageSections.Tests;

/// <summary>The raw button target of the tests, as an editor enters it: a page by key, or an external address.</summary>
/// <param name="PageKey">The key of a page.</param>
/// <param name="ExternalLink">An external address.</param>
public sealed record NavLink(string? PageKey, string? ExternalLink);
