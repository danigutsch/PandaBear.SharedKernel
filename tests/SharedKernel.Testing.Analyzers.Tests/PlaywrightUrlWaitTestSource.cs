namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Builds sources with minimal stand-ins for Playwright's page and frame and for the SharedKernel address wait, for the SKTEST020 tests.</summary>
internal static class PlaywrightUrlWaitTestSource
{
    /// <summary>The stand-in Playwright API: <c>IPage</c> and <c>IFrame</c> with the three <c>WaitForURLAsync</c> overloads, and an unrelated type with a same-named method.</summary>
    public const string Playwright = """
        namespace Microsoft.Playwright
        {
            public sealed class PageWaitForURLOptions { }
            public interface IPage
            {
                IFrame MainFrame { get; }
                Task WaitForURLAsync(string url, PageWaitForURLOptions? options = default);
                Task WaitForURLAsync(System.Text.RegularExpressions.Regex url, PageWaitForURLOptions? options = default);
                Task WaitForURLAsync(Func<string, bool> url, PageWaitForURLOptions? options = default);
            }
            public interface IFrame
            {
                Task WaitForURLAsync(string url, object? options = default);
            }
        }
        namespace Demo
        {
            public sealed class Router
            {
                public Task WaitForURLAsync(string url) => Task.CompletedTask;
            }
        }
        """;

    /// <summary>The stand-in SharedKernel address wait.</summary>
    public const string ExpectPath = """
        namespace PandaBear.Testing.Playwright
        {
            public static class PlaywrightPageExtensions
            {
                public static Task ExpectPath(this Microsoft.Playwright.IPage page, Uri expected) => Task.CompletedTask;
                public static Task ExpectPath(this Microsoft.Playwright.IPage page, Uri baseAddress, string relativePath) => Task.CompletedTask;
            }
        }
        """;

    /// <summary>Returns a class whose method has a page, a frame, a router, a base address and a path in scope and then runs the statement.</summary>
    /// <param name="statement">The statement at the end of the method body.</param>
    /// <param name="withExpectPath">True to include the stand-in SharedKernel address wait.</param>
    /// <returns>The source.</returns>
    public static string Create(string statement, bool withExpectPath = true) => $$"""
        namespace Demo
        {
            public sealed class Workflow
            {
                public async Task Run(Microsoft.Playwright.IPage page, Uri baseAddress, string path, Router router)
                {
                    var frame = page.MainFrame;
                    {{statement}}
                }
            }
        }
        {{Playwright}}
        {{(withExpectPath ? ExpectPath : string.Empty)}}
        """;
}
