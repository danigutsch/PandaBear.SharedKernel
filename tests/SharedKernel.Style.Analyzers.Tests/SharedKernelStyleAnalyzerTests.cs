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
    public async Task With_image_tag_without_with_image_sha256_reports_skstyle005()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AppHost
            {
                public void Configure(dynamic builder)
                {
                    builder.AddPostgres("database").WithImageTag("18.4");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AspireImageTagAndDigest);
        Assert.Contains("WithImageTag", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_image_tag_with_with_image_sha256_does_not_report_skstyle005()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AppHost
            {
                public void Configure(dynamic builder)
                {
                    builder.AddPostgres("database")
                        .WithImageTag("18.4")
                        .WithImageSHA256("4aabea78cf39b90e834caf3af7d602a18565f6fe2508705c8d01aa63245c2e20");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AspireImageTagAndDigest);
    }

    [Fact]
    public async Task With_image_sha256_without_with_image_tag_reports_skstyle005()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AppHost
            {
                public void Configure(dynamic builder)
                {
                    builder.AddRedis("cache")
                        .WithImageSHA256("2838d5524559494f6f1cd66e97e76b200d64a633a8614200620755ed395daf32");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AspireImageTagAndDigest);
        Assert.Contains("WithImageSHA256", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_image_sha256_value_with_sha256_prefix_reports_skstyle005()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AppHost
            {
                public void Configure(dynamic builder)
                {
                    builder.AddRedis("cache")
                        .WithImageTag("8.8")
                        .WithImageSHA256("sha256:2838d5524559494f6f1cd66e97e76b200d64a633a8614200620755ed395daf32");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AspireImageTagAndDigest);
        Assert.Contains("bare 64-character digest", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Companion_resource_image_pins_are_analyzed_independently()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AppHost
            {
                public void Configure(dynamic builder)
                {
                    builder.AddPostgres("database")
                        .WithImageTag("18.4")
                        .WithImageSHA256("4aabea78cf39b90e834caf3af7d602a18565f6fe2508705c8d01aa63245c2e20")
                        .WithPgWeb(pgweb => pgweb.WithImageSHA256("a5256d416e2e8b92d69a4459058e3eca33a9f075d8325491644411d0bc3bd70b"));
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AspireImageTagAndDigest);
        Assert.Contains("WithImageSHA256", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostgreSQL_redis_and_companion_image_pins_with_tags_do_not_report_skstyle005()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AppHost
            {
                public void Configure(dynamic builder)
                {
                    builder.AddPostgres("database")
                        .WithImageTag("18.4")
                        .WithImageSHA256("4aabea78cf39b90e834caf3af7d602a18565f6fe2508705c8d01aa63245c2e20")
                        .WithPgWeb(pgweb => pgweb
                            .WithImageTag("0.17.0")
                            .WithImageSHA256("a5256d416e2e8b92d69a4459058e3eca33a9f075d8325491644411d0bc3bd70b"));

                    builder.AddRedis("cache")
                        .WithImageTag("8.8")
                        .WithImageSHA256("2838d5524559494f6f1cd66e97e76b200d64a633a8614200620755ed395daf32")
                        .WithRedisInsight(redisInsight => redisInsight
                            .WithImageTag("3.6")
                            .WithImageSHA256("aa21bbd198455b4ad964f76782db951155aa0d712321f599972d1525f031f0e6"));
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == StyleDiagnosticIds.AspireImageTagAndDigest);
    }

}
