using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Builds analyzer fixtures against the Playwright browser interfaces in metadata.</summary>
internal static class PlaywrightFixedDelayTestSource
{
    /// <summary>Diagnostic ID for the fixed browser delay rule.</summary>
    internal const string DiagnosticId = "SKTEST024";

    private const string PlaywrightSource = """
        namespace Microsoft.Playwright;

        public interface IPage
        {
            IFrame MainFrame { get; }
            ILocator Locator(string selector);
            void SetDefaultTimeout(float timeout);
            Task WaitForTimeoutAsync(float timeout);
        }

        public interface IFrame
        {
            Task WaitForTimeoutAsync(float timeout);
            Task<IJSHandle> WaitForFunctionAsync(string expression, object? arg = default, FrameWaitForFunctionOptions? options = default);
        }

        public interface IJSHandle;

        public interface ILocator
        {
            Task WaitForAsync(LocatorWaitForOptions? options = default);
        }

        public sealed class FrameWaitForFunctionOptions
        {
            public float? Timeout { get; set; }
        }

        public sealed class LocatorWaitForOptions
        {
            public float? Timeout { get; set; }
        }
        """;

    /// <summary>Compiles a browser test body with the requested diagnostic severity and source path.</summary>
    internal static CSharpCompilation Compile(
        string statement,
        ReportDiagnostic? severity = ReportDiagnostic.Warn,
        string path = "TestSource.cs",
        string playwrightAssembly = "Microsoft.Playwright")
    {
        var playwright = AnalyzerTestHarness.CreateCompilation(PlaywrightSource, assemblyName: playwrightAssembly);
        using var image = new MemoryStream();
        playwright.Emit(image, cancellationToken: TestContext.Current.CancellationToken).Success.ShouldBeTrue();
        var compilation = AnalyzerTestHarness.CreateCompilation($$"""
            using Microsoft.Playwright;
            using Page = Microsoft.Playwright.IPage;

            public sealed class BrowserTests
            {
                public async Task Exercise(Page page, FakePage fake, float duration, CancellationToken cancellationToken)
                {
                    var frame = page.MainFrame;
                    {{statement}}
                }
            }

            public sealed class FakePage
            {
                public Task WaitForTimeoutAsync(float duration) => Task.CompletedTask;
            }
            """, path: path).AddReferences(MetadataReference.CreateFromImage(image.ToArray()));

        return severity is { } level
            ? compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
                compilation.Options.SpecificDiagnosticOptions.SetItem(DiagnosticId, level)))
            : compilation;
    }
}
