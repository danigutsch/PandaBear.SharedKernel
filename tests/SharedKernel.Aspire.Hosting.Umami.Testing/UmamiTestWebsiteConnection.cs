namespace PandaBear.Aspire.Hosting.Umami.Testing;

/// <summary>The Umami website that a test assembly sends page views to, as its connection string names it.</summary>
/// <param name="ConnectionString">The website's connection string, <c>Endpoint={Umami's base address};WebsiteId={id}</c>, which a site reads.</param>
/// <param name="Endpoint">Umami's base address, where the tests read the statistics back.</param>
/// <param name="WebsiteId">The website id.</param>
internal sealed record UmamiTestWebsiteConnection(string ConnectionString, Uri Endpoint, Guid WebsiteId);
