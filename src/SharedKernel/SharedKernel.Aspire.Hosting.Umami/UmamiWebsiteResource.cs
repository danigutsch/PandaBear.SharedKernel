namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A website in an Umami resource, shown under it. Its connection string is <c>Endpoint={Umami's base address};WebsiteId={id}</c>,
/// the form the Umami page-view target of <c>PandaBear.SharedKernel.AspNetCore.WebAnalytics</c> reads, so a site gets everything with
/// <c>WithReference(website, connectionName: "umami")</c>. In run mode the website is created in Umami as soon as Umami is ready.
/// </summary>
/// <param name="name">The name of the resource, which is also the website's name in Umami.</param>
/// <param name="domain">The website's domain, such as <c>example.com</c>, which Umami uses to keep the site's own pages out of the referrers.</param>
/// <param name="umami">The Umami resource the website belongs to.</param>
public sealed class UmamiWebsiteResource([ResourceName] string name, string domain, UmamiResource umami) : Resource(name), IResourceWithConnectionString
{
    /// <summary>Gets the Umami resource the website belongs to.</summary>
    public UmamiResource Umami { get; } = umami;

    /// <summary>Gets the website's domain.</summary>
    public string Domain { get; } = domain;

    /// <summary>
    /// Gets the website id: by default a name-based UUID (RFC 9562, version 5) of the Umami resource name, the website name and the
    /// domain, so the same AppHost finds the same website in a kept database on every run without storing anything.
    /// </summary>
    public Guid WebsiteId { get; internal set; } = UmamiWebsiteIds.Create(umami.Name, name, domain);

    /// <summary>Gets <c>Endpoint={Umami's base address};WebsiteId={id}</c>, without credentials.</summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"Endpoint={Umami.PrimaryEndpoint.Property(EndpointProperty.Url)};WebsiteId={WebsiteId.ToString("D")}");
}
