namespace PandaBear.IO.Analyzers.Tests;

/// <summary>
/// Tests that SKIO003 reports upload and raw-body endpoints without an endpoint-level request size limit.
/// </summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.StreamBufferingCapability)]
public sealed class RequestSizeLimitAnalyzerTests
{
    private const string Usings = """
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.Http;
        using Microsoft.AspNetCore.Http.Features;
        using Microsoft.AspNetCore.Mvc;
        using System.IO.Pipelines;

        """;

    private static readonly string[] Enabled = [IODiagnosticIds.MissingRequestSizeLimit];

    [Theory]
    [InlineData("[HttpPost] public IActionResult Post(IFormFile file) => Ok();")]
    [InlineData("[HttpPost] public IActionResult Post(IFormFileCollection files) => Ok();")]
    [InlineData("[HttpPost] public IActionResult Post([FromForm] IFormFile file, int id) => Ok();")]
    [InlineData("[HttpPost] public async Task<IActionResult> Post() { await Request.Body.CopyToAsync(Stream.Null); return Ok(); }")]
    [InlineData("[HttpPost] public async Task<IActionResult> Post() { using var reader = new StreamReader(HttpContext.Request.Body); return Ok(await reader.ReadLineAsync()); }")]
    [InlineData("[RequestFormLimits(ValueLengthLimit = 1)] [HttpPost] public IActionResult Post(IFormFile file) => Ok();")]
    public async Task Controller_action_without_size_limit_reports_skio003(string member)
    {
        // Arrange
        var source = Usings + $$"""
            namespace Demo;

            public sealed class UploadController : ControllerBase
            {
                {{member}}
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, enabledDiagnostics: Enabled);

        // Assert
        diagnostics.ShouldContain(candidate => candidate.Id == IODiagnosticIds.MissingRequestSizeLimit);
    }

    [Theory]
    [InlineData("[RequestSizeLimit(1000)] [HttpPost] public IActionResult Post(IFormFile file) => Ok();")]
    [InlineData("[DisableRequestSizeLimit] [HttpPost] public IActionResult Post(IFormFile file) => Ok();")]
    [InlineData("[RequestFormLimits(MultipartBodyLengthLimit = 1000)] [HttpPost] public IActionResult Post(IFormFile file) => Ok();")]
    [InlineData("[RequestSizeLimit(1000)] [HttpPost] public async Task<IActionResult> Post() { await Request.Body.CopyToAsync(Stream.Null); return Ok(); }")]
    [InlineData("[HttpPost] public IActionResult Post() { var limit = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>(); return Ok(limit); }")]
    [InlineData("[HttpPost] public IActionResult Post(int id) => Ok();")]
    [InlineData("[HttpGet] public IActionResult Get() => Ok(Request.Path.Value);")]
    [InlineData("private IActionResult Helper(IFormFile file) => Ok();")]
    public async Task Controller_action_with_limit_or_without_intake_does_not_report_skio003(string member)
    {
        // Arrange
        var source = Usings + $$"""
            namespace Demo;

            public sealed class UploadController : ControllerBase
            {
                {{member}}
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, enabledDiagnostics: Enabled);

        // Assert
        diagnostics.ShouldNotContain(candidate => candidate.Id == IODiagnosticIds.MissingRequestSizeLimit);
    }

    [Fact]
    public async Task Controller_with_type_level_limit_does_not_report_skio003()
    {
        // Arrange
        var source = Usings + """
            namespace Demo;

            [RequestSizeLimit(1000)]
            public sealed class UploadController : ControllerBase
            {
                [HttpPost]
                public IActionResult Post(IFormFile file) => Ok();
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, enabledDiagnostics: Enabled);

        // Assert
        diagnostics.ShouldNotContain(candidate => candidate.Id == IODiagnosticIds.MissingRequestSizeLimit);
    }

    [Theory]
    [InlineData("app.MapPost(\"/u\", (IFormFile file) => Results.Ok());")]
    [InlineData("app.MapPut(\"/u\", (IFormFileCollection files) => Results.Ok());")]
    [InlineData("app.MapPost(\"/u\", (PipeReader reader) => Results.Ok());")]
    [InlineData("app.MapPost(\"/u\", (Stream body) => Results.Ok());")]
    [InlineData("app.MapPost(\"/u\", async (HttpRequest request) => { await request.Body.CopyToAsync(Stream.Null); return Results.Ok(); });")]
    [InlineData("app.MapPost(\"/u\", Handle);")]
    [InlineData("app.MapPost(\"/u\", (IFormFile file) => Results.Ok()).WithName(\"u\");")]
    public async Task Mapped_endpoint_without_size_limit_reports_skio003(string statement)
    {
        // Arrange
        var source = Usings + $$"""
            namespace Demo;

            public static class Endpoints
            {
                public static IResult Handle(IFormFile file) => Results.Ok();

                public static void Map(WebApplication app)
                {
                    {{statement}}
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, enabledDiagnostics: Enabled);

        // Assert
        diagnostics.ShouldContain(candidate => candidate.Id == IODiagnosticIds.MissingRequestSizeLimit);
    }

    [Theory]
    [InlineData("app.MapPost(\"/u\", [RequestSizeLimit(1000)] (IFormFile file) => Results.Ok());")]
    [InlineData("app.MapPost(\"/u\", [DisableRequestSizeLimit] (IFormFile file) => Results.Ok());")]
    [InlineData("app.MapPost(\"/u\", (IFormFile file) => Results.Ok()).WithMetadata(new RequestSizeLimitAttribute(1000));")]
    [InlineData("app.MapPost(\"/u\", (IFormFile file) => Results.Ok()).WithName(\"u\").WithMetadata(new RequestSizeLimitAttribute(1000));")]
    [InlineData("app.MapPost(\"/u\", HandleLimited);")]
    [InlineData("app.MapPost(\"/u\", (HttpContext context) => { var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>(); return Results.Ok(limit); });")]
    [InlineData("app.MapPost(\"/u\", (string name) => Results.Ok(name));")]
    [InlineData("app.MapGet(\"/u\", (HttpRequest request) => Results.Ok(request.Body));")]
    public async Task Mapped_endpoint_with_limit_or_without_intake_does_not_report_skio003(string statement)
    {
        // Arrange
        var source = Usings + $$"""
            namespace Demo;

            public static class Endpoints
            {
                [RequestSizeLimit(1000)]
                public static IResult HandleLimited(IFormFile file) => Results.Ok();

                public static void Map(WebApplication app)
                {
                    {{statement}}
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, enabledDiagnostics: Enabled);

        // Assert
        diagnostics.ShouldNotContain(candidate => candidate.Id == IODiagnosticIds.MissingRequestSizeLimit);
    }

    [Fact]
    public async Task Rule_is_disabled_by_default()
    {
        // Arrange
        var source = Usings + """
            namespace Demo;

            public sealed class UploadController : ControllerBase
            {
                [HttpPost]
                public IActionResult Post(IFormFile file) => Ok();
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(candidate => candidate.Id == IODiagnosticIds.MissingRequestSizeLimit);
    }

    [Fact]
    public async Task Ordinary_class_taking_form_file_does_not_report_skio003()
    {
        // Arrange
        var source = Usings + """
            namespace Demo;

            public sealed class Importer
            {
                public void Import(IFormFile file) { }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, enabledDiagnostics: Enabled);

        // Assert
        diagnostics.ShouldNotContain(candidate => candidate.Id == IODiagnosticIds.MissingRequestSizeLimit);
    }
}
