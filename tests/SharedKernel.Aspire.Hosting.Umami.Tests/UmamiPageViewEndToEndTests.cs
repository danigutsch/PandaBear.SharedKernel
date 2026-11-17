using System.Net;

namespace PandaBear.Aspire.Hosting.Umami.Tests;

/// <summary>
/// Proves the zero-configuration path end to end with real containers: Umami runs with the website that its website resource set up, and
/// a page view sent through the Umami page-view target with the website's connection string is recorded by Umami.
/// </summary>
/// <param name="umami">The Umami website of the assembly, borrowed from the shared test-services host or started once for the assembly.</param>
public sealed class UmamiPageViewEndToEndTests(UmamiWebsiteFixture umami)
{
    [Fact]
    public async Task A_page_view_sent_with_the_connection_string_of_the_website_the_apphost_set_up_is_recorded_by_umami()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var website = await umami.GetWebsite(ct);
        var before = await UmamiStatistics.PageViews(website.Endpoint, website.WebsiteId, ct);
        await using var site = await PageViewTestSite.Start(website.ConnectionString, ct);

        // Act
        using var page = await site.Visit("/pt/viagens?utm_source=newsletter&email=visitor%40example.com", ct);
        await site.Stop(ct);

        // Assert
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = await UmamiStatistics.PageViews(website.Endpoint, website.WebsiteId, ct);
        after.ShouldBe(before + 1, "a shared Umami may already hold page views of earlier runs of this test");
    }
}
