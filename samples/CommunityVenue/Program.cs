using System.Security.Cryptography;
using CommunityVenue;
using CommunityVenue.Components;
using PandaBear.AspNetCore;
using PandaBear.AspNetCore.Seo;
using PandaBear.Blazor.Site.Theming;
using PandaBear.HttpCaching.AspNetCore;
using PandaBear.IO;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.WebHost.UseStaticWebAssets();

var contentRoot = builder.Environment.ContentRootPath;
var pages = VenueCatalog.Import(Path.Combine(contentRoot, "Fixtures", "pages.json"));
var mediaRoot = Path.Combine(contentRoot, "media");
Directory.CreateDirectory(mediaRoot);
string imageName;
await using (var image = File.OpenRead(Path.Combine(contentRoot, "Fixtures", "hall.webp")))
{
    imageName = Convert.ToHexStringLower(await SHA256.HashDataAsync(image)) + ".webp";
    image.Position = 0;
    await AtomicFile.CreateIfAbsent(Path.Combine(mediaRoot, imageName),
        async (destination, ct) => await image.CopyToAsync(destination, ct));
}

var theme = SiteThemeStylesheet.RenderVersioned(SiteTheme.Default);
var origin = SitemapCanonicalOrigin.Parse(builder.Configuration["CanonicalOrigin"] ?? "https://riverbank.example");
builder.Services.AddSingleton(new VenueSite(pages, origin, imageName, theme.Version));

var app = builder.Build();
app.UseAntiforgery();
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/theme.css")
    {
        HttpCacheHeaders.SetPublic(context, TimeSpan.FromMinutes(1));
    }

    await next(context);
});

app.MapGet("/", (VenueSite site) => Results.Redirect(site.DefaultPathFor("visit")));
app.MapGet("/visit", (VenueSite site) => Results.Redirect(site.DefaultPathFor("visit")));
app.MapGet("/workshops", (VenueSite site) => Results.Redirect(site.DefaultPathFor("workshops")));
app.MapVersionedStylesheet("/theme.css", _ => ValueTask.FromResult(theme));
app.MapImmutableFiles("/media", mediaRoot, new Dictionary<string, string>(StringComparer.Ordinal) { [".webp"] = "image/webp" },
    (_, name) => ValueTask.FromResult(string.Equals(name, imageName, StringComparison.Ordinal)));
app.MapRobotsTxt($"User-agent: *\nAllow: /\nDisallow: /editor\nSitemap: {new Uri(origin, "/sitemap.xml").AbsoluteUri}\n");
app.MapMethods("/sitemap.xml", [HttpMethods.Get, HttpMethods.Head], async (VenueSite site, CancellationToken ct) =>
    Results.Stream(await SitemapXmlSerializer.Serialize(site.Sitemap(), ct), SitemapXmlSerializer.ContentType));
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
