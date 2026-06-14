using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, options);

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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AsyncSuffix);
    }

    [Fact]
    public async Task CancellationToken_Parameter_Name_Reports_SKSTYLE002()
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
        Assert.Contains("cancellationToken", diagnostic.GetMessage(global::System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationToken_Parameter_Named_Ct_Does_Not_Report_SKSTYLE002()
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
    public async Task CancellationToken_Parameter_Default_Value_Reports_SKSTYLE003()
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
        Assert.Contains("ct", diagnostic.GetMessage(global::System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Local_Function_CancellationToken_Parameter_Name_Reports_SKSTYLE002()
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
    public async Task Lambda_CancellationToken_Parameter_Name_Reports_SKSTYLE002()
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
    public void Parse_Uses_SyntaxTree_Options_Before_Global_Options()
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
    public void Parse_Falls_Back_To_Default_When_Value_Is_Invalid()
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
    public async Task Multiple_Top_Level_Types_Per_File_Report_SKSTYLE004()
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
        Assert.Contains("TourWriter", diagnostic.GetMessage(global::System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partial_Types_Are_Excluded_From_SKSTYLE004()
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
    public async Task Nested_Types_Do_Not_Report_SKSTYLE004()
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
    public async Task Nested_Namespaces_Still_Report_SKSTYLE004()
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

    private sealed class StyleTestAnalyzerConfigOptionsProvider(
        ImmutableDictionary<string, string>? globalOptions = null,
        ImmutableDictionary<SyntaxTree, ImmutableDictionary<string, string>>? syntaxTreeOptions = null)
        : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions global = new StyleTestAnalyzerConfigOptions(globalOptions);
        private readonly ImmutableDictionary<SyntaxTree, ImmutableDictionary<string, string>> perTreeOptions = syntaxTreeOptions
            ?? ImmutableDictionary<SyntaxTree, ImmutableDictionary<string, string>>.Empty;

        public override AnalyzerConfigOptions GlobalOptions => global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return perTreeOptions.TryGetValue(tree, out var values)
                ? new StyleTestAnalyzerConfigOptions(values)
                : StyleTestAnalyzerConfigOptions.Empty;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => StyleTestAnalyzerConfigOptions.Empty;
    }

    private sealed class StyleTestAnalyzerConfigOptions(ImmutableDictionary<string, string>? values) : AnalyzerConfigOptions
    {
        public static readonly StyleTestAnalyzerConfigOptions Empty = new(null);

        private readonly ImmutableDictionary<string, string> options = values ?? ImmutableDictionary<string, string>.Empty;

        public override bool TryGetValue(string key, out string value)
        {
            if (options.TryGetValue(key, out var candidateValue))
            {
                value = candidateValue;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }

}
