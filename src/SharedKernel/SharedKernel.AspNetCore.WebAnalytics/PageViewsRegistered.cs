namespace SharedKernel.AspNetCore.WebAnalytics;

/// <summary>Marks that <see cref="PageViewHostApplicationBuilderExtensions.AddPageViews"/> ran, so <c>UsePageViews</c> can tell a missing registration from a site without targets.</summary>
internal sealed class PageViewsRegistered;
