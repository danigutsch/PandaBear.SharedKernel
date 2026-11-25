using SharedKernel.BuildingBlocks;

namespace SharedKernel.Blazor.Site.Tests.CulturePages;

/// <summary>The content of one language that the culture page tests read.</summary>
/// <param name="Culture">The language the content was read for.</param>
public sealed record CulturePageContent(ContentCulture Culture);
