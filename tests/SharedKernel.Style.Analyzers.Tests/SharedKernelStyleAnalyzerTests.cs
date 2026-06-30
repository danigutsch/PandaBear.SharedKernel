using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SharedKernel.Style.Analyzers.Tests;

public sealed class SharedKernelStyleAnalyzerTests
{
    [Fact]
    public async Task Method_ending_with_async_reports_skstyle001()
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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
        Assert.Contains("LoadAsync", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains("Load", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Override_ending_with_async_does_not_report_by_default()
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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
    }

    [Fact]
    public async Task Interface_implementation_ending_with_async_does_not_report_by_default()
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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
    }

    [Fact]
    public async Task Interface_implementation_ending_with_async_reports_when_configured()
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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, options);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
    }

    [Fact]
    public async Task Custom_abstract_async_contract_still_reports_diagnostic()
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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
    }

    [Fact]
    public async Task Custom_interface_async_contract_still_reports_diagnostic()
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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
    }

    [Fact]
    public async Task CancellationToken_parameter_name_reports_skstyle002()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(\"VT-42\");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.CancellationTokenParameterName);
        Assert.Contains("cancellationToken", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationToken_parameter_named_ct_does_not_report_skstyle002()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(CancellationToken ct)
                {
                    ct.ThrowIfCancellationRequested();
                    return Task.FromResult(\"VT-42\");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.CancellationTokenParameterName);
    }

    [Fact]
    public async Task CancellationToken_parameter_default_value_reports_skstyle003()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(CancellationToken ct = default)
                {
                    return Task.FromResult(\"VT-42\");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.CancellationTokenDefaultValue);
        Assert.Contains("ct", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Local_function_cancellationtoken_parameter_name_reports_skstyle002()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Build()
                {
                    return Execute(cancellationToken: default);

                    static async Task<string> Execute(CancellationToken cancellationToken)
                    {
                        await Task.Yield();
                        cancellationToken.ThrowIfCancellationRequested();
                        return \"VT-42\";
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.CancellationTokenParameterName);
    }

    [Fact]
    public async Task Lambda_cancellationtoken_parameter_name_reports_skstyle002()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Func<CancellationToken, Task<string>> Build()
                {
                    return async cancellationToken =>
                    {
                        await Task.Yield();
                        cancellationToken.ThrowIfCancellationRequested();
                        return "VT-42";
                    };
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.CancellationTokenParameterName);
    }

    [Fact]
    public void Parse_uses_syntaxtree_options_before_global_options()
    {
        // Arrange
        var syntaxTree = CSharpSyntaxTree.ParseText(
            "class Demo { }",
            new CSharpParseOptions(LanguageVersion.Preview),
            cancellationToken: TestContext.Current.CancellationToken);
        var provider = new StyleTestAnalyzerConfigOptionsProvider(
            globalOptions: ImmutableDictionary<string, string>.Empty
                .Add("sharedkernel_style_allow_async_suffix_interface_implementations", "true"),
            syntaxTreeOptions: ImmutableDictionary<SyntaxTree, ImmutableDictionary<string, string>>.Empty
                .Add(
                    syntaxTree,
                    ImmutableDictionary<string, string>.Empty
                        .Add("sharedkernel_style_allow_async_suffix_interface_implementations", "false")));

        // Act
        var options = StyleAnalyzerConfigOptions.Parse(provider, syntaxTree);

        // Assert
        Assert.True(options.AllowAsyncSuffixOverrides);
        Assert.False(options.AllowAsyncSuffixInterfaceImplementations);
    }

    [Fact]
    public void Parse_falls_back_to_default_when_value_is_invalid()
    {
        // Arrange
        var provider = new StyleTestAnalyzerConfigOptionsProvider(
            globalOptions: ImmutableDictionary<string, string>.Empty
                .Add("sharedkernel_style_allow_async_suffix_overrides", "not-a-bool"));

        // Act
        var options = StyleAnalyzerConfigOptions.Parse(provider, syntaxTree: null);

        // Assert
        Assert.True(options.AllowAsyncSuffixOverrides);
        Assert.True(options.AllowAsyncSuffixInterfaceImplementations);
    }

    [Fact]
    public async Task Multiple_top_level_types_per_file_report_skstyle004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
            }

            public sealed class TourWriter
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.MultipleTopLevelTypesPerFile);
        Assert.Contains("TourWriter", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Single_top_level_type_per_file_does_not_report_skstyle004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.MultipleTopLevelTypesPerFile);
    }

    [Fact]
    public async Task File_local_helper_type_with_top_level_type_reports_skstyle004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
            }

            file static class MappingInputs
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.MultipleTopLevelTypesPerFile);
        Assert.Contains("MappingInputs", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partial_types_are_excluded_from_skstyle004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed partial class TourLoader
            {
            }

            public sealed partial class TourLoader
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.MultipleTopLevelTypesPerFile);
    }

    [Fact]
    public async Task Partial_type_with_non_partial_helper_still_reports_skstyle004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed partial class TourLoader
            {
            }

            file static class TourLoaderHelpers
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.MultipleTopLevelTypesPerFile);
        Assert.Contains("TourLoaderHelpers", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generated_files_do_not_report_skstyle004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class GeneratedOne
            {
            }

            public sealed class GeneratedTwo
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, path: "GeneratedModels.g.cs");

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.MultipleTopLevelTypesPerFile);
    }

    [Fact]
    public async Task Nested_types_do_not_report_skstyle004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                private sealed class NestedHelper
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.MultipleTopLevelTypesPerFile);
    }

    [Fact]
    public async Task Nested_namespaces_still_report_skstyle004()
    {
        // Arrange
        const string source = """
            namespace Demo
            {
                namespace Inner
                {
                    public sealed class TourLoader
                    {
                    }

                    public sealed class TourWriter
                    {
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.MultipleTopLevelTypesPerFile);
    }

    [Fact]
    public async Task Broad_operation_cancelled_exception_filter_reports_skstyle006()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Consumer
            {
                public void Handle(CancellationToken ct)
                {
                    try
                    {
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
    }

    [Fact]
    public async Task Negated_operation_cancelled_exception_filter_reports_skstyle006()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Consumer
            {
                public void Handle(CancellationToken ct)
                {
                    try
                    {
                    }
                    catch (Exception ex) when (!(ex is System.OperationCanceledException))
                    {
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
    }

    [Fact]
    public async Task Operation_cancelled_exception_filter_with_token_check_does_not_report_skstyle006()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Consumer
            {
                public void Handle(CancellationToken ct)
                {
                    try
                    {
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                    {
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
    }

    [Fact]
    public async Task Operation_cancelled_exception_filter_with_different_token_check_reports_skstyle006()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Consumer
            {
                public void Handle(CancellationToken ct, CancellationToken otherToken)
                {
                    try
                    {
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException || !otherToken.IsCancellationRequested)
                    {
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
    }

    [Fact]
    public async Task Operation_cancelled_exception_filter_with_positive_token_check_reports_skstyle006()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Consumer
            {
                public void Handle(CancellationToken ct)
                {
                    try
                    {
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException || ct.IsCancellationRequested)
                    {
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
    }

    [Fact]
    public async Task Operation_cancelled_exception_filter_inside_local_function_reports_skstyle006()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Consumer
            {
                public void Handle()
                {
                    void Run(CancellationToken ct)
                    {
                        try
                        {
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                        }
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
    }

    [Fact]
    public async Task Operation_cancelled_exception_filter_inside_lambda_reports_skstyle006()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Consumer
            {
                public void Handle()
                {
                    Action<CancellationToken> run = ct =>
                    {
                        try
                        {
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                        }
                    };
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
    }

    [Fact]
    public async Task Operation_cancelled_exception_filter_with_helper_does_not_report_skstyle006()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Consumer
            {
                public void Handle(CancellationToken ct)
                {
                    try
                    {
                    }
                    catch (Exception ex) when (ex.ShouldHandleAsFailure(ct))
                    {
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
    }

    [Fact]
    public async Task Broad_operation_cancelled_exception_filter_without_ct_reports_skstyle006()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Consumer
            {
                public void Handle(CancellationToken cancellationToken)
                {
                    try
                    {
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
    }

    [Fact]
    public async Task Direct_logger_extension_call_reports_skstyle007()
    {
        // Arrange
        const string source = """
            namespace Microsoft.Extensions.Logging
            {
                public interface ILogger
                {
                }

                public static class LoggerExtensions
                {
                    public static void LogInformation(this ILogger logger, string message)
                    {
                    }
                }
            }

            namespace Demo
            {
                using Microsoft.Extensions.Logging;

                public sealed class Consumer(Microsoft.Extensions.Logging.ILogger logger)
                {
                    public void Handle()
                    {
                        logger.LogInformation("Handled item");
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = Assert.Single(
            diagnostics,
            static candidate => candidate.Id == StyleDiagnosticIds.NonSourceGeneratedLogging);
        Assert.Contains(
            "LogInformation",
            diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Direct_logger_interface_call_reports_skstyle007()
    {
        // Arrange
        const string source = """
            namespace Microsoft.Extensions.Logging
            {
                public enum LogLevel
                {
                    Information
                }

                public readonly struct EventId
                {
                }

                public interface ILogger
                {
                    void Log<TState>(
                        LogLevel logLevel,
                        EventId eventId,
                        TState state,
                        Exception? exception,
                        Func<TState, Exception?, string> formatter);
                }
            }

            namespace Demo
            {
                public sealed class Consumer(Microsoft.Extensions.Logging.ILogger logger)
                {
                    public void Handle()
                    {
                        logger.Log(
                            Microsoft.Extensions.Logging.LogLevel.Information,
                            default,
                            "Handled item",
                            null,
                            static (state, _) => state);
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = Assert.Single(
            diagnostics,
            static candidate => candidate.Id == StyleDiagnosticIds.NonSourceGeneratedLogging);
        Assert.Contains(
            "Log",
            diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Source_generated_logger_method_call_does_not_report_skstyle007()
    {
        // Arrange
        const string source = """
            namespace Microsoft.Extensions.Logging
            {
                public interface ILogger
                {
                }
            }

            namespace Demo
            {
                public sealed class Consumer(Microsoft.Extensions.Logging.ILogger logger)
                {
                    public void Handle()
                    {
                        logger.ItemHandled();
                    }
                }

                public static class ConsumerLog
                {
                    public static void ItemHandled(this Microsoft.Extensions.Logging.ILogger logger)
                    {
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.NonSourceGeneratedLogging);
    }

    [Fact]
    public async Task Non_logger_method_named_like_logging_does_not_report_skstyle007()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AuditSink
            {
                public void LogInformation(string message)
                {
                }
            }

            public sealed class Consumer(AuditSink sink)
            {
                public void Handle()
                {
                    sink.LogInformation("Handled item");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.NonSourceGeneratedLogging);
    }

}
