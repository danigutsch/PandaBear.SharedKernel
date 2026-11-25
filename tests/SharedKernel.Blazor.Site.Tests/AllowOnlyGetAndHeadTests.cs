using System.Net;
using SharedKernel.Blazor.Site.Tests.Methods;
using SharedKernel.Testing;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>
/// Tests the pages of a site without forms: they answer GET and HEAD, and any other method 405 with <c>Allow: GET, HEAD</c>, while the
/// error page, which the error handlers re-execute with the failed request's method, keeps every method.
/// </summary>
[TestScope(TestScope.Integration)]
public sealed class AllowOnlyGetAndHeadTests
{
    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task A_page_answers_get_and_head(string method)
    {
        // Arrange
        await using var host = await MethodsHost.Start();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/methods");

        // Act
        using var response = await host.Client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task A_page_refuses_any_other_method_and_names_the_allowed_ones(string method)
    {
        // Arrange
        await using var host = await MethodsHost.Start();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/methods");

        // Act
        using var response = await host.Client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        string.Join(", ", response.Content.Headers.Allow).ShouldBe("GET, HEAD");
    }

    [Fact]
    public async Task The_pages_declare_get_and_head_and_the_error_page_no_method()
    {
        // Arrange
        await using var host = await MethodsHost.Start();

        // Act
        var page = host.EndpointOf<MethodsPage>().Metadata.OfType<IHttpMethodMetadata>().Select(static metadata => metadata.HttpMethods).ToList();
        var errorPage = host.EndpointOf<MethodsErrorPage>().Metadata.OfType<IHttpMethodMetadata>().ToList();

        // Assert
        page.ShouldHaveSingleItem().ShouldBe(["GET", "HEAD"]);
        errorPage.ShouldBeEmpty();
    }

    [Fact]
    public void Arguments_are_checked()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRazorComponents();
        using var app = builder.Build();
        var components = app.MapRazorComponents<MethodsPage>();
        var withoutBuilder = () => RazorComponentMethodExtensions.AllowOnlyGetAndHead(null!);
        var nullTypes = () => components.AllowOnlyGetAndHead(null!);
        var nullType = () => components.AllowOnlyGetAndHead(typeof(MethodsErrorPage), null!);

        // Act & Assert
        withoutBuilder.ShouldThrow<ArgumentNullException>();
        nullTypes.ShouldThrow<ArgumentNullException>();
        nullType.ShouldThrow<ArgumentException>();
    }
}
