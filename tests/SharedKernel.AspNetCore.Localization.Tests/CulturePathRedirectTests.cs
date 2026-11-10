using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using static PandaBear.AspNetCore.Localization.Tests.LocalizationTestCultures;

namespace PandaBear.AspNetCore.Localization.Tests;

/// <summary>Tests the culture path redirect middleware and the remembered-choice cookie.</summary>
public sealed class CulturePathRedirectTests
{
    [Fact]
    public async Task Redirects_the_root_to_the_default_culture_without_other_signals()
    {
        // Arrange
        await using var app = await CulturePathRedirectTestApp.Start(Supported);

        // Act
        var context = await CulturePathRedirectTestApp.Send(app, "GET", "/");

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
        context.Response.Headers.Location.ToString().ShouldBe("/pt/");
        context.Response.Headers.Vary.ToString().ShouldBe("Accept-Language, Cookie");
    }

    [Theory]
    [InlineData("en-US,en;q=0.9", "/en/")]
    [InlineData("fr, en;q=0.5, pt;q=0.4", "/en/")]
    [InlineData("fr, pt;q=0.4, en;q=0.9", "/en/")]
    [InlineData("en;q=0, pt", "/pt/")]
    [InlineData("fr-FR, de;q=0.8, *;q=0.1", "/pt/")]
    [InlineData("", "/pt/")]
    public async Task Uses_accept_language_when_there_is_no_cookie(string acceptLanguage, string expected)
    {
        // Arrange
        await using var app = await CulturePathRedirectTestApp.Start(Supported);

        // Act
        var context = await CulturePathRedirectTestApp.Send(
            app,
            "GET",
            "/",
            request =>
            {
                if (acceptLanguage.Length > 0)
                {
                    request.Headers.AcceptLanguage = acceptLanguage;
                }
            });

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
        context.Response.Headers.Location.ToString().ShouldBe(expected);
    }

    [Fact]
    public async Task Prefers_the_remembered_cookie_over_accept_language()
    {
        // Arrange
        await using var app = await CulturePathRedirectTestApp.Start(Supported);

        // Act
        var context = await CulturePathRedirectTestApp.Send(
            app,
            "GET",
            "/",
            request =>
            {
                request.Headers.Cookie = $"{CulturePathRedirectExtensions.DefaultCookieName}=en";
                request.Headers.AcceptLanguage = "pt";
            });

        // Assert
        context.Response.Headers.Location.ToString().ShouldBe("/en/");
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("../evil")]
    [InlineData("")]
    public async Task Ignores_an_invalid_cookie_value(string cookie)
    {
        // Arrange
        await using var app = await CulturePathRedirectTestApp.Start(Supported);

        // Act
        var context = await CulturePathRedirectTestApp.Send(
            app,
            "GET",
            "/",
            request =>
            {
                request.Headers.Cookie = $"{CulturePathRedirectExtensions.DefaultCookieName}={cookie}";
                request.Headers.AcceptLanguage = "en";
            });

        // Assert
        context.Response.Headers.Location.ToString().ShouldBe("/en/");
    }

    [Fact]
    public async Task Keeps_the_path_and_query_when_adding_the_prefix()
    {
        // Arrange
        await using var app = await CulturePathRedirectTestApp.Start(Supported);

        // Act
        var context = await CulturePathRedirectTestApp.Send(
            app,
            "GET",
            "/tours/42",
            request => request.QueryString = new QueryString("?a=1&b=x%20y"));

        // Assert
        context.Response.Headers.Location.ToString().ShouldBe("/pt/tours/42?a=1&b=x%20y");
    }

    [Fact]
    public async Task Keeps_the_path_base()
    {
        // Arrange
        await using var app = await CulturePathRedirectTestApp.Start(Supported);

        // Act
        var context = await CulturePathRedirectTestApp.Send(app, "GET", "/tours", request => request.PathBase = "/app");

        // Assert
        context.Response.Headers.Location.ToString().ShouldBe("/app/pt/tours");
    }

    [Fact]
    public async Task Treats_an_unsupported_prefix_as_a_missing_one()
    {
        // Arrange
        await using var app = await CulturePathRedirectTestApp.Start(Supported);

        // Act
        var context = await CulturePathRedirectTestApp.Send(app, "GET", "/fr/tours");

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
        context.Response.Headers.Location.ToString().ShouldBe("/pt/fr/tours");
    }

    [Theory]
    [InlineData("//evil.example/x")]
    [InlineData("/\\evil.example/x")]
    [InlineData("///evil")]
    public async Task Never_redirects_to_another_host(string path)
    {
        // Arrange
        await using var app = await CulturePathRedirectTestApp.Start(Supported);

        // Act
        var context = await CulturePathRedirectTestApp.Send(app, "GET", path, request => request.Headers.AcceptLanguage = "en");

        // Assert
        var location = context.Response.Headers.Location.ToString();
        location.ShouldStartWith("/en/", StringComparison.Ordinal);
        new Uri(new Uri("http://localhost/"), location).Host.ShouldBe("localhost");
    }

    [Theory]
    [InlineData("/pt")]
    [InlineData("/en/tours")]
    [InlineData("/EN/tours")]
    public async Task Passes_prefixed_requests_through(string path)
    {
        // Arrange
        await using var app = await CulturePathRedirectTestApp.Start(Supported);

        // Act
        var context = await CulturePathRedirectTestApp.Send(app, "GET", path);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        context.Response.Headers.Location.Count.ShouldBe(0);
        context.Response.Headers.Vary.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Passes_non_get_requests_through(string method)
    {
        // Arrange
        await using var app = await CulturePathRedirectTestApp.Start(Supported);

        // Act
        var context = await CulturePathRedirectTestApp.Send(app, method, "/tours");

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Redirects_head_requests()
    {
        // Arrange
        await using var app = await CulturePathRedirectTestApp.Start(Supported);

        // Act
        var context = await CulturePathRedirectTestApp.Send(app, "HEAD", "/tours");

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
    }

    [Theory]
    [InlineData("/robots.txt")]
    [InlineData("/css/site.css")]
    [InlineData("/favicon.ico")]
    public async Task Passes_paths_with_a_file_extension_through(string path)
    {
        // Arrange
        await using var app = await CulturePathRedirectTestApp.Start(Supported);

        // Act
        var context = await CulturePathRedirectTestApp.Send(app, "GET", path);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData("/api", true)]
    [InlineData("/api/tours", true)]
    [InlineData("/API/tours", true)]
    [InlineData("/apiary", false)]
    [InlineData("/tours", false)]
    public async Task Passes_excluded_prefixes_through_on_segment_boundaries(string path, bool excluded)
    {
        // Arrange
        await using var app = await CulturePathRedirectTestApp.Start(Supported, excludedPrefixes: [new PathString("/api")]);

        // Act
        var context = await CulturePathRedirectTestApp.Send(app, "GET", path);

        // Assert
        context.Response.StatusCode.ShouldBe(excluded ? StatusCodes.Status200OK : StatusCodes.Status302Found);
    }

    [Fact]
    public async Task Reads_a_custom_cookie_name()
    {
        // Arrange
        await using var app = await CulturePathRedirectTestApp.Start(Supported, "lang");

        // Act
        var context = await CulturePathRedirectTestApp.Send(app, "GET", "/", request => request.Headers.Cookie = "lang=en");

        // Assert
        context.Response.Headers.Location.ToString().ShouldBe("/en/");
    }

    [Fact]
    public void Rejects_invalid_arguments()
    {
        // Arrange
        var app = WebApplication.CreateBuilder().Build();

        // Act
        var nullApp = () => { CulturePathRedirectExtensions.UseCulturePathRedirect(null!, Supported); };
        var nullCultures = () => { app.UseCulturePathRedirect(null!); };
        var blankCookie = () => { app.UseCulturePathRedirect(Supported, " "); };

        // Assert
        nullApp.ShouldThrow<ArgumentNullException>();
        nullCultures.ShouldThrow<ArgumentNullException>();
        blankCookie.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void RememberCulture_writes_the_culture_as_a_lax_http_only_cookie()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        context.Response.RememberCulture(En, Supported);

        // Assert
        var setCookie = SetCookieHeaderValue.Parse(context.Response.Headers.SetCookie.ToString());
        setCookie.Name.Value.ShouldBe(CulturePathRedirectExtensions.DefaultCookieName);
        setCookie.Value.Value.ShouldBe("en");
        setCookie.HttpOnly.ShouldBeTrue();
        setCookie.SameSite.ShouldBe(Microsoft.Net.Http.Headers.SameSiteMode.Lax);
        setCookie.Path.Value.ShouldBe("/");
        setCookie.MaxAge.ShouldNotBeNull();
    }

    [Fact]
    public void RememberCulture_rejects_an_unsupported_culture()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var remember = () => { context.Response.RememberCulture(Fr, Supported); };

        // Assert
        remember.ShouldThrow<ArgumentException>();
        context.Response.Headers.SetCookie.Count.ShouldBe(0);
    }
}
