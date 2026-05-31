using System.Collections.Immutable;

namespace SharedKernel.Style.Analyzers.Tests;

public sealed class SharedKernelStyleAnalyzerTests
{
    [Fact]
    public async Task Method_Ending_With_Async_Reports_SKSTYLE001()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public async Task<string> LoadAsync(CancellationToken ct)
                {
                    await Task.Yield();
                    return "VT-42";
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
        Assert.Contains("LoadAsync", diagnostic.GetMessage(global::System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains("Load", diagnostic.GetMessage(global::System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Override_Ending_With_Async_Does_Not_Report_By_Default()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AsyncReader : StringReader
            {
                public override async Task<string?> ReadLineAsync()
                {
                    await Task.Yield();
                    return string.Empty;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
    }

    [Fact]
    public async Task Interface_Implementation_Ending_With_Async_Does_Not_Report_By_Default()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AsyncLifecycle : IAsyncDisposable
            {
                public ValueTask DisposeAsync()
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
    }

    [Fact]
    public async Task Interface_Implementation_Ending_With_Async_Reports_When_Configured()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AsyncLifecycle : IAsyncDisposable
            {
                public ValueTask DisposeAsync()
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;
        var options = ImmutableDictionary<string, string>.Empty
            .Add("sharedkernel_style_allow_async_suffix_interface_implementations", "false");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(source, options);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
    }

    [Fact]
    public async Task Custom_Abstract_Async_Contract_Still_Reports_Diagnostic()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public abstract class BackgroundWorker
            {
                protected abstract Task ExecuteAsync(CancellationToken ct);
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
    }

    [Fact]
    public async Task Custom_Interface_Async_Contract_Still_Reports_Diagnostic()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public interface IAsyncLifecycle
            {
                ValueTask DisposeAsync();
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
    }
}
