using System.Data.Common;
using System.Net;

namespace PandaBear.Aspire.Hosting.Umami.Tests;

/// <summary>
/// Proves the zero-configuration path end to end with real containers: the AppHost model starts Umami, sets the website up, and a page
/// view sent through the Umami page-view target with the website's connection string is recorded by Umami.
/// </summary>
public sealed class UmamiPageViewEndToEndTests
{
    [Fact]
    public async Task A_page_view_sent_with_the_connection_string_of_the_website_the_apphost_set_up_is_recorded_by_umami()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var application = await UmamiTestApplication.Start(ct);
        var connectionString = await application.GetConnectionString(UmamiTestApplication.Website, ct);
        var websiteId = Guid.Parse((string)new DbConnectionStringBuilder { ConnectionString = connectionString }["WebsiteId"]);
        await using var site = await PageViewTestSite.Start(connectionString, ct);

        // Act
        using var page = await site.Visit("/pt/viagens?utm_source=newsletter&email=visitor%40example.com", ct);
        await site.Stop(ct);

        // Assert
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        var umami = application.GetEndpoint(UmamiTestApplication.Umami, "http");
        var pageViews = await UmamiStatistics.PageViews(umami, websiteId, ct);
        pageViews.ShouldBe(1);
    }
}
