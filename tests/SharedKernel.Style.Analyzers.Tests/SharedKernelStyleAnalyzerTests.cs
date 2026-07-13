using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SharedKernel.Style.Analyzers.Tests;

public sealed class SharedKernelStyleAnalyzerTests
{
    [Fact]
    public void Supported_diagnostics_match_style_descriptor_contract()
    {
        // Arrange
        var analyzer = new SharedKernelStyleAnalyzer();
        string[] expectedDiagnosticIds =
        [
            StyleDiagnosticIds.AsyncSuffix,
            StyleDiagnosticIds.CancellationTokenParameterName,
            StyleDiagnosticIds.CancellationTokenDefaultValue,
            StyleDiagnosticIds.MultipleTopLevelTypesPerFile,
            StyleDiagnosticIds.GenericTypeNameSuffix,
            StyleDiagnosticIds.BroadOperationCanceledExceptionFilter,
            StyleDiagnosticIds.NonSourceGeneratedLogging,
            StyleDiagnosticIds.DomainEventSuffix,
            StyleDiagnosticIds.SuccessOnlyResultMethod,
            StyleDiagnosticIds.OptionalResultMethod,
        ];

        // Act
        var diagnosticIds = analyzer.SupportedDiagnostics.Select(static descriptor => descriptor.Id).OrderBy(static id => id, StringComparer.Ordinal);

        // Assert
        (diagnosticIds).ShouldBe(expectedDiagnosticIds.OrderBy(static id => id, StringComparer.Ordinal));
    }

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
        var diagnostic = (diagnostics).ShouldHaveSingleItem(static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
        (diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).ShouldContain("LoadAsync", StringComparison.Ordinal);
        (diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).ShouldContain("Load", StringComparison.Ordinal);
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
        (diagnostics).ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
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
        (diagnostics).ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
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
        (diagnostics).ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
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
        (diagnostics).ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
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
        (diagnostics).ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
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
        var diagnostic = (diagnostics).ShouldHaveSingleItem(static candidate => candidate.Id == StyleDiagnosticIds.CancellationTokenParameterName);
        (diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).ShouldContain("cancellationToken", StringComparison.Ordinal);
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
        (diagnostics).ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.CancellationTokenParameterName);
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
        var diagnostic = (diagnostics).ShouldHaveSingleItem(static candidate => candidate.Id == StyleDiagnosticIds.CancellationTokenDefaultValue);
        (diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).ShouldContain("ct", StringComparison.Ordinal);
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
        (diagnostics).ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.CancellationTokenParameterName);
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
        (diagnostics).ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.CancellationTokenParameterName);
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
        (options.AllowAsyncSuffixOverrides).ShouldBeTrue();
        (options.AllowAsyncSuffixInterfaceImplementations).ShouldBeFalse();
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
        (options.AllowAsyncSuffixOverrides).ShouldBeTrue();
        (options.AllowAsyncSuffixInterfaceImplementations).ShouldBeTrue();
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
        var diagnostic = (diagnostics).ShouldHaveSingleItem(static candidate => candidate.Id == StyleDiagnosticIds.MultipleTopLevelTypesPerFile);
        (diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).ShouldContain("TourWriter", StringComparison.Ordinal);
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
        (diagnostics).ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.MultipleTopLevelTypesPerFile);
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
        var diagnostic = (diagnostics).ShouldHaveSingleItem(static candidate => candidate.Id == StyleDiagnosticIds.MultipleTopLevelTypesPerFile);
        (diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).ShouldContain("MappingInputs", StringComparison.Ordinal);
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
        (diagnostics).ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.MultipleTopLevelTypesPerFile);
    }

    [Fact]
    public async Task Generic_type_ending_with_generic_reports_skstyle005()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Result
            {
            }

            public sealed class ResultGeneric<T>
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.Where(static candidate => candidate.Id == StyleDiagnosticIds.GenericTypeNameSuffix)
            .ShouldHaveSingleItem();
        var message = diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture);
        message.ShouldContain("ResultGeneric", StringComparison.Ordinal);
        message.ShouldContain("Result", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generic_type_ending_with_oft_reports_skstyle005()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class ResultOfT<T>
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.Where(static candidate => candidate.Id == StyleDiagnosticIds.GenericTypeNameSuffix)
            .ShouldHaveSingleItem();
        var message = diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture);
        message.ShouldContain("ResultOfT", StringComparison.Ordinal);
        message.ShouldContain("Result", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generic_type_ending_with_misspelled_gereric_reports_skstyle005()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class ResultGereric<T>
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.Where(static candidate => candidate.Id == StyleDiagnosticIds.GenericTypeNameSuffix)
            .ShouldHaveSingleItem();
        var message = diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture);
        message.ShouldContain("ResultGereric", StringComparison.Ordinal);
        message.ShouldContain("Result", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_generic_type_ending_with_generic_does_not_report_skstyle005()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Generic
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.GenericTypeNameSuffix);
    }

    [Fact]
    public async Task Generic_type_without_generic_suffix_does_not_report_skstyle005()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Result<T>
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.GenericTypeNameSuffix);
    }

    [Fact]
    public async Task Nested_generic_type_with_generic_suffix_does_not_report_skstyle005()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class ResultFactory
            {
                public sealed class ResultOfT<T>
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.GenericTypeNameSuffix);
    }

    [Fact]
    public async Task Generic_interface_struct_and_delegate_suffixes_report_skstyle005()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public interface IResultGeneric<T>
            {
            }

            public readonly struct ResultStructOfT<T>
            {
            }

            public delegate void ResultHandlerGereric<T>(T value);
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var messages = diagnostics
            .Where(static candidate => candidate.Id == StyleDiagnosticIds.GenericTypeNameSuffix)
            .Select(static diagnostic => diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        messages.Length.ShouldBe(3);
        messages.ShouldContain(message => message.Contains("IResultGeneric", StringComparison.Ordinal));
        messages.ShouldContain(message => message.Contains("ResultStructOfT", StringComparison.Ordinal));
        messages.ShouldContain(message => message.Contains("ResultHandlerGereric", StringComparison.Ordinal));
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
        var diagnostic = (diagnostics).ShouldHaveSingleItem(static candidate => candidate.Id == StyleDiagnosticIds.MultipleTopLevelTypesPerFile);
        (diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).ShouldContain("TourLoaderHelpers", StringComparison.Ordinal);
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
        (diagnostics).ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.MultipleTopLevelTypesPerFile);
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
        (diagnostics).ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.MultipleTopLevelTypesPerFile);
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
        (diagnostics).ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.MultipleTopLevelTypesPerFile);
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
        (diagnostics).ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
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
        (diagnostics).ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
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
        (diagnostics).ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
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
        (diagnostics).ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
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
        (diagnostics).ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
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
        (diagnostics).ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
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
        (diagnostics).ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
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
        (diagnostics).ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
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
        (diagnostics).ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter);
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
        var diagnostic = (diagnostics).ShouldHaveSingleItem(static candidate => candidate.Id == StyleDiagnosticIds.NonSourceGeneratedLogging);
        (diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).ShouldContain("LogInformation", StringComparison.Ordinal);
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
        var diagnostic = (diagnostics).ShouldHaveSingleItem(static candidate => candidate.Id == StyleDiagnosticIds.NonSourceGeneratedLogging);
        (diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).ShouldContain("Log", StringComparison.Ordinal);
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
        (diagnostics).ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.NonSourceGeneratedLogging);
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
        (diagnostics).ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.NonSourceGeneratedLogging);
    }

    [Fact]
    public async Task Domain_event_without_suffix_reports_skstyle008()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Domain
            {
                public interface IDomainEvent;
            }

            namespace Demo
            {
                public sealed record TourCreated(Guid TourId) : SharedKernel.Domain.IDomainEvent;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.Where(static candidate => candidate.Id == StyleDiagnosticIds.DomainEventSuffix)
            .ShouldHaveSingleItem();
        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
            .ShouldContain("TourCreated", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Domain_event_with_suffix_does_not_report_skstyle008()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Domain
            {
                public interface IDomainEvent;
            }

            namespace Demo
            {
                public sealed record TourCreatedDomainEvent(Guid TourId) : SharedKernel.Domain.IDomainEvent;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.DomainEventSuffix);
    }

    [Fact]
    public async Task Domain_event_notification_generic_type_does_not_report_skstyle008()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Domain
            {
                public interface IDomainEvent;
            }

            namespace Demo
            {
                public sealed class DomainEventNotification<TDomainEvent>
                    where TDomainEvent : SharedKernel.Domain.IDomainEvent
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.DomainEventSuffix);
    }

    [Fact]
    public async Task Nested_domain_event_without_suffix_does_not_report_skstyle008()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Domain
            {
                public interface IDomainEvent;
            }

            namespace Demo
            {
                public sealed class TourEvents
                {
                    public sealed record Created(System.Guid TourId) : SharedKernel.Domain.IDomainEvent;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.DomainEventSuffix);
    }

    [Theory]
    [InlineData("Ok")]
    [InlineData("NoContent")]
    [InlineData("Accepted")]
    public async Task Success_only_result_method_reports_skstyle009(string factoryName)
    {
        // Arrange
        var source = $$"""
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result Ok() => default;
                    public static Result NoContent() => default;
                    public static Result Accepted() => default;
                }
            }

            namespace Demo
            {
                public sealed partial class ThemeSettings
                {
                    public SharedKernel.Results.Result Replace(bool useAlternate)
                    {
                        if (useAlternate)
                        {
                            return SharedKernel.Results.Result.{{factoryName}}();
                        }

                        return SharedKernel.Results.Result.{{factoryName}}();
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem(static candidate => candidate.Id == StyleDiagnosticIds.SuccessOnlyResultMethod);
        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
            .ShouldContain("cannot return a failure Result", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Task")]
    [InlineData("ValueTask")]
    public async Task Async_success_only_result_method_reports_skstyle009(string taskType)
    {
        // Arrange
        var source = $$"""
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result Ok() => default;
                }
            }

            namespace Demo
            {
                public sealed partial class ThemeSettings
                {
                    public async {{taskType}}<SharedKernel.Results.Result> Replace()
                    {
                        await Task.Yield();
                        return SharedKernel.Results.Result.Ok();
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.SuccessOnlyResultMethod);
    }

    [Theory]
    [InlineData("Task")]
    [InlineData("ValueTask")]
    public async Task Async_success_only_generic_result_method_reports_skstyle009(string taskType)
    {
        // Arrange
        var source = $$"""
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result<T> Ok<T>(T value) => default;
                }

                public readonly partial struct Result<T>
                {
                }
            }

            namespace Demo
            {
                public sealed partial class ThemeSettings
                {
                    public async {{taskType}}<SharedKernel.Results.Result<string>> Find()
                    {
                        await Task.Yield();
                        return SharedKernel.Results.Result.Ok("Theme");
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.SuccessOnlyResultMethod);
    }

    [Fact]
    public async Task Expression_bodied_success_only_result_method_reports_skstyle009()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result Ok() => default;
                }
            }

            namespace Demo
            {
                public sealed partial class ThemeSettings
                {
                    public SharedKernel.Results.Result Replace() => SharedKernel.Results.Result.Ok();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.SuccessOnlyResultMethod);
    }

    [Fact]
    public async Task Result_method_with_failure_path_does_not_report_skstyle009()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result Ok() => default;
                    public static Result Error(string detail) => default;
                }
            }

            namespace Demo
            {
                public sealed partial class ThemeSettings
                {
                    public SharedKernel.Results.Result Replace(bool isValid) =>
                        isValid ? SharedKernel.Results.Result.Ok() : SharedKernel.Results.Result.Error("Theme is invalid.");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.SuccessOnlyResultMethod);
    }

    [Theory]
    [InlineData("Task")]
    [InlineData("ValueTask")]
    public async Task Async_result_method_with_failure_path_does_not_report_skstyle009(string taskType)
    {
        // Arrange
        var source = $$"""
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result Ok() => default;
                    public static Result Error(string detail) => default;
                }
            }

            namespace Demo
            {
                public sealed partial class ThemeSettings
                {
                    public async {{taskType}}<SharedKernel.Results.Result> Replace(bool isValid)
                    {
                        await Task.Yield();
                        return isValid
                            ? SharedKernel.Results.Result.Ok()
                            : SharedKernel.Results.Result.Error("Theme is invalid.");
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.SuccessOnlyResultMethod);
    }

    [Fact]
    public async Task Generated_success_only_async_result_method_does_not_report_skstyle009()
    {
        // Arrange
        const string source = """
            // <auto-generated/>
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result Ok() => default;
                }
            }

            namespace Demo
            {
                public sealed partial class ThemeSettings
                {
                    public async Task<SharedKernel.Results.Result> Replace()
                    {
                        await Task.Yield();
                        return SharedKernel.Results.Result.Ok();
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, path: "GeneratedModels.g.cs");

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.SuccessOnlyResultMethod);
    }

    [Fact]
    public async Task Result_method_returning_propagated_result_does_not_report_skstyle009()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result Error(string detail) => default;
                }
            }

            namespace Demo
            {
                public sealed partial class ThemeSettings
                {
                    public SharedKernel.Results.Result Replace() => Validate();

                    private static SharedKernel.Results.Result Validate() =>
                        SharedKernel.Results.Result.Error("Theme is invalid.");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.SuccessOnlyResultMethod);
    }

    [Fact]
    public async Task Conditional_success_only_result_method_reports_skstyle009()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result Ok() => default;
                    public static Result NoContent() => default;
                }
            }

            namespace Demo
            {
                public sealed partial class ThemeSettings
                {
                    public SharedKernel.Results.Result Replace(bool isChanged) =>
                        isChanged ? SharedKernel.Results.Result.Ok() : SharedKernel.Results.Result.NoContent();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.SuccessOnlyResultMethod);
    }

    [Fact]
    public async Task Switch_expression_result_methods_report_only_when_all_arms_succeed()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result Ok() => default;
                    public static Result NoContent() => default;
                    public static Result Error(string detail) => default;
                }
            }

            namespace Demo
            {
                public sealed partial class ThemeSettings
                {
                    public SharedKernel.Results.Result Replace(int status) => status switch
                    {
                        0 => SharedKernel.Results.Result.Ok(),
                        _ => SharedKernel.Results.Result.NoContent(),
                    };

                    public SharedKernel.Results.Result Validate(int status) => status switch
                    {
                        0 => SharedKernel.Results.Result.Ok(),
                        _ => SharedKernel.Results.Result.Error("Theme is invalid."),
                    };
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem(static candidate => candidate.Id == StyleDiagnosticIds.SuccessOnlyResultMethod);
        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
            .ShouldContain("Replace", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unreachable_result_failure_path_does_not_suppress_skstyle009()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result Ok() => default;
                    public static Result Error(string detail) => default;
                }
            }

            namespace Demo
            {
                public sealed partial class ThemeSettings
                {
                    public SharedKernel.Results.Result Replace()
                    {
                        if (false)
                        {
                            return SharedKernel.Results.Result.Error("Unreachable");
                        }

                        return SharedKernel.Results.Result.Ok();
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.SuccessOnlyResultMethod);
    }

    [Fact]
    public async Task Result_interface_implementation_does_not_report_skstyle009()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result Ok() => default;
                }
            }

            namespace Demo
            {
                public partial interface IThemeSettings
                {
                    SharedKernel.Results.Result Replace();
                }

                public sealed partial class ThemeSettings : IThemeSettings
                {
                    public SharedKernel.Results.Result Replace() => SharedKernel.Results.Result.Ok();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.SuccessOnlyResultMethod);
    }

    [Fact]
    public async Task Result_override_does_not_report_skstyle009()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result Ok() => default;
                }
            }

            namespace Demo
            {
                public abstract partial class ThemeSettings
                {
                    public abstract SharedKernel.Results.Result Replace();
                }

                public sealed partial class PublicThemeSettings : ThemeSettings
                {
                    public override SharedKernel.Results.Result Replace() => SharedKernel.Results.Result.Ok();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.SuccessOnlyResultMethod);
    }

    [Fact]
    public async Task Generic_result_method_with_ok_and_not_found_paths_reports_skstyle010()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result<T> Ok<T>(T value) => default;
                    public static Result<T> NotFound<T>(string detail) => default;
                }

                public readonly partial struct Result<T>
                {
                }
            }

            namespace Demo
            {
                public sealed partial class ThemeSettings
                {
                    public SharedKernel.Results.Result<string> Find(bool exists)
                    {
                        if (exists)
                        {
                            return SharedKernel.Results.Result.Ok("Theme");
                        }

                        return SharedKernel.Results.Result.NotFound<string>("Theme was not found.");
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.OptionalResultMethod);
    }

    [Fact]
    public async Task Generic_result_method_without_both_ok_and_not_found_paths_does_not_report_skstyle010()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result<T> Ok<T>(T value) => default;
                }

                public readonly partial struct Result<T>
                {
                }
            }

            namespace Demo
            {
                public sealed partial class ThemeSettings
                {
                    public SharedKernel.Results.Result<string> Find() => SharedKernel.Results.Result.Ok("Theme");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.OptionalResultMethod);
        diagnostics.ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.SuccessOnlyResultMethod);
    }

    [Fact]
    public async Task Generic_result_method_returning_created_only_reports_skstyle009()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result<T> Created<T>(T value) => default;
                }

                public readonly partial struct Result<T>
                {
                }
            }

            namespace Demo
            {
                public sealed partial class ThemeSettings
                {
                    public SharedKernel.Results.Result<string> Create() => SharedKernel.Results.Result.Created("Theme");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldContain(static candidate => candidate.Id == StyleDiagnosticIds.SuccessOnlyResultMethod);
    }

    [Fact]
    public async Task Conditional_and_switch_generic_result_methods_report_skstyle010()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result<T> Ok<T>(T value) => default;
                    public static Result<T> NotFound<T>(string detail) => default;
                }

                public readonly partial struct Result<T>
                {
                }
            }

            namespace Demo
            {
                public sealed partial class ThemeSettings
                {
                    public SharedKernel.Results.Result<string> Find(bool exists) =>
                        exists ? SharedKernel.Results.Result.Ok("Theme") : SharedKernel.Results.Result.NotFound<string>("Theme was not found.");

                    public SharedKernel.Results.Result<string> FindByStatus(int status) => status switch
                    {
                        0 => SharedKernel.Results.Result.Ok("Theme"),
                        _ => SharedKernel.Results.Result.NotFound<string>("Theme was not found."),
                    };
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var optionalResultDiagnostics = diagnostics.Where(static candidate => candidate.Id == StyleDiagnosticIds.OptionalResultMethod).ToArray();
        optionalResultDiagnostics.Length.ShouldBe(2);
    }

    [Fact]
    public async Task Unreachable_or_contract_or_non_direct_generic_result_methods_do_not_report_skstyle010()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Results
            {
                public readonly partial struct Result
                {
                    public static Result<T> Ok<T>(T value) => default;
                    public static Result<T> NotFound<T>(string detail) => default;
                    public static Result<T> Error<T>(string detail) => default;
                }

                public readonly partial struct Result<T>
                {
                }
            }

            namespace Demo
            {
                public partial interface IThemeSettings
                {
                    SharedKernel.Results.Result<string> Find();
                }

                public abstract partial class ThemeSettingsBase
                {
                    public abstract SharedKernel.Results.Result<string> FindById();
                }

                public sealed partial class ThemeSettings : ThemeSettingsBase, IThemeSettings
                {
                    public SharedKernel.Results.Result<string> Find()
                    {
                        if (false)
                        {
                            return SharedKernel.Results.Result.NotFound<string>("Unreachable");
                        }

                        return SharedKernel.Results.Result.Ok("Theme");
                    }

                    public override SharedKernel.Results.Result<string> FindById() =>
                        SharedKernel.Results.Result.NotFound<string>("Theme was not found.");

                    public SharedKernel.Results.Result<string> Propagated() => External();

                    public System.Threading.Tasks.Task<SharedKernel.Results.Result<string>> FindAsync() =>
                        System.Threading.Tasks.Task.FromResult(SharedKernel.Results.Result.Ok("Theme"));

                    public SharedKernel.Results.Result<string> Throws() => throw new System.InvalidOperationException();

                    private static SharedKernel.Results.Result<string> External() =>
                        SharedKernel.Results.Result.Error<string>("Theme failed.");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.OptionalResultMethod);
    }

}
