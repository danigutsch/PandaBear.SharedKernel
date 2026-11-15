using System.Net;

namespace PandaBear.Forgejo.Tests;

/// <summary>Tests how every page of a Forgejo list is read and when reading stops.</summary>
public sealed class ForgejoPagerTests
{
    private const string Next = """<https://forge.example/api/v1/x?limit=50&page=2>; rel="next",<https://forge.example/api/v1/x?limit=50&page=9>; rel="last" """;
    private const string Last = """<https://forge.example/api/v1/x?limit=50&page=1>; rel="first",<https://forge.example/api/v1/x?limit=50&page=1>; rel="prev" """;

    [Fact]
    public async Task A_link_header_decides_even_when_a_page_is_short()
    {
        // Arrange
        var pages = new ScriptedPages([ScriptedPages.Page(3, link: Next), ScriptedPages.Page(2, link: Next), ScriptedPages.Page(1, link: Last)]);

        // Act
        var items = await ForgejoPager.ReadAll(pages.Read, 10, "the list", TestContext.Current.CancellationToken);

        // Assert
        items.Count.ShouldBe(6);
        pages.Requested.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Without_a_link_header_the_total_count_decides()
    {
        // Arrange
        var pages = new ScriptedPages([ScriptedPages.Page(50, total: 120), ScriptedPages.Page(50, total: 120), ScriptedPages.Page(20, total: 120)]);

        // Act
        var items = await ForgejoPager.ReadAll(pages.Read, 10, "the list", TestContext.Current.CancellationToken);

        // Assert
        items.Count.ShouldBe(120);
        pages.Requested.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task The_total_count_decides_when_the_instance_pages_by_fewer_than_fifty()
    {
        // Arrange
        // An instance with max_response_items 20 answers limit=50 with 20 items.
        var pages = new ScriptedPages([ScriptedPages.Page(20, total: 45), ScriptedPages.Page(20, total: 45), ScriptedPages.Page(5, total: 45)]);

        // Act
        var items = await ForgejoPager.ReadAll(pages.Read, 10, "the list", TestContext.Current.CancellationToken);

        // Assert
        items.Count.ShouldBe(45);
        pages.Requested.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Without_paging_headers_a_short_page_is_the_last()
    {
        // Arrange
        var pages = new ScriptedPages([ScriptedPages.Page(50), ScriptedPages.Page(49)]);

        // Act
        var items = await ForgejoPager.ReadAll(pages.Read, 10, "the list", TestContext.Current.CancellationToken);

        // Assert
        items.Count.ShouldBe(99);
        pages.Requested.ShouldBe([1, 2]);
    }

    [Fact]
    public async Task An_empty_page_is_the_last_whatever_the_headers_say()
    {
        // Arrange
        var pages = new ScriptedPages([ScriptedPages.Page(50, total: 500, link: Next), ScriptedPages.Page(0, total: 500, link: Next)]);

        // Act
        var items = await ForgejoPager.ReadAll(pages.Read, 10, "the list", TestContext.Current.CancellationToken);

        // Assert
        items.Count.ShouldBe(50);
        pages.Requested.ShouldBe([1, 2]);
    }

    [Fact]
    public async Task A_list_longer_than_the_page_cap_fails_instead_of_being_cut()
    {
        // Arrange
        var pages = new ScriptedPages([ScriptedPages.Page(50, total: 1000), ScriptedPages.Page(50, total: 1000), ScriptedPages.Page(50, total: 1000)]);

        // Act
        Func<Task> read = () => ForgejoPager.ReadAll(pages.Read, 2, "The releases of owner/name", TestContext.Current.CancellationToken);

        // Assert
        var exception = await read.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe("The releases of owner/name span more than 2 pages of 50 items; nothing was read from a partial list.");
        pages.Requested.ShouldBe([1, 2]);
    }

    [Fact]
    public void A_page_reads_its_link_and_total_count_from_the_response()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("Link", Next.Trim());
        response.Headers.TryAddWithoutValidation("X-Total-Count", "123");

        // Act
        var page = ForgejoPager.Page([1, 2], response.Headers);

        // Assert
        page.Items.ShouldBe([1, 2]);
        page.Link.ShouldBe(Next.Trim());
        page.TotalCount.ShouldBe(123);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("")]
    public void A_total_count_that_is_not_a_count_is_ignored(string total)
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("X-Total-Count", total);

        // Act
        var page = ForgejoPager.Page<int>([], response.Headers);

        // Assert
        page.TotalCount.ShouldBeNull();
        page.Link.ShouldBeNull();
    }
}
