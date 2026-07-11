extern alias styleanalyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace SharedKernel.Style.CodeFixes.Tests;

public sealed class SharedKernelStyleCodeFixProviderTests
{
    [Fact]
    public async Task Async_suffix_fix_renames_method_and_reference()
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

                public Task<string> Execute(CancellationToken ct)
                {
                    return LoadAsync(ct);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.AsyncSuffix, "LoadAsync(CancellationToken ct)");

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        (updatedText).ShouldContain("Load(CancellationToken ct)", StringComparison.Ordinal);
        (updatedText).ShouldContain("return Load(ct);", StringComparison.Ordinal);
        (updatedText).ShouldNotContain("LoadAsync", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Async_suffix_fix_is_not_offered_when_target_name_would_conflict()
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

                public Task<string> Load(CancellationToken ct)
                {
                    return Task.FromResult("existing");
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.AsyncSuffix, "LoadAsync(CancellationToken ct)");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        (codeActions).ShouldBeEmpty();
    }

    [Fact]
    public async Task Async_suffix_fix_is_not_offered_when_base_type_already_defines_target_name()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public abstract class BaseLoader
            {
                public virtual Task<string> Load(CancellationToken ct)
                {
                    return Task.FromResult("base");
                }
            }

            public sealed class TourLoader : BaseLoader
            {
                public async Task<string> LoadAsync(CancellationToken ct)
                {
                    await Task.Yield();
                    return "VT-42";
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.AsyncSuffix, "LoadAsync(CancellationToken ct)");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        (codeActions).ShouldBeEmpty();
    }

    [Fact]
    public async Task Async_suffix_fix_regroups_overloads_when_rename_would_split_overload_group()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(string route, CancellationToken ct)
                {
                    return Task.FromResult(route);
                }

                public Task<string> Execute(CancellationToken ct)
                {
                    return LoadAsync(ct);
                }

                public async Task<string> LoadAsync(CancellationToken ct)
                {
                    await Task.Yield();
                    return "VT-42";
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.AsyncSuffix, "LoadAsync(CancellationToken ct)");

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        (updatedText).ShouldContain("public async Task<string> Load(CancellationToken ct)", StringComparison.Ordinal);
        (updatedText).ShouldContain("public Task<string> Load(string route, CancellationToken ct)", StringComparison.Ordinal);
        (updatedText).ShouldContain("return Load(ct);", StringComparison.Ordinal);
        var executeIndex = updatedText.IndexOf("public Task<string> Execute", StringComparison.Ordinal);
        var renamedOverloadIndex = updatedText.IndexOf("public async Task<string> Load(CancellationToken ct)", StringComparison.Ordinal);
        var existingOverloadIndex = updatedText.IndexOf("public Task<string> Load(string route, CancellationToken ct)", StringComparison.Ordinal);
        (executeIndex >= 0).ShouldBeTrue();
        (renamedOverloadIndex >= 0).ShouldBeTrue();
        (existingOverloadIndex >= 0).ShouldBeTrue();
        (renamedOverloadIndex < existingOverloadIndex).ShouldBeTrue();
        (existingOverloadIndex < executeIndex).ShouldBeTrue();
    }

    [Fact]
    public async Task Async_suffix_fix_regroups_overloads_when_earlier_references_shift_declaration_position()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                private const string Prefix = nameof(LoadAsync) + nameof(LoadAsync);

                public Task<string> Load(string route, CancellationToken ct)
                {
                    return Task.FromResult(route + Prefix);
                }

                public Task<string> Execute(CancellationToken ct)
                {
                    return LoadAsync(ct);
                }

                public async Task<string> LoadAsync(CancellationToken ct)
                {
                    await Task.Yield();
                    return Prefix;
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.AsyncSuffix, "LoadAsync(CancellationToken ct)");

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        (updatedText).ShouldNotContain("nameof(LoadAsync)", StringComparison.Ordinal);
        (updatedText).ShouldContain("nameof(Load)", StringComparison.Ordinal);
        var executeIndex = updatedText.IndexOf("public Task<string> Execute", StringComparison.Ordinal);
        var renamedOverloadIndex = updatedText.IndexOf("public async Task<string> Load(CancellationToken ct)", StringComparison.Ordinal);
        var existingOverloadIndex = updatedText.IndexOf("public Task<string> Load(string route, CancellationToken ct)", StringComparison.Ordinal);
        (executeIndex >= 0).ShouldBeTrue();
        (renamedOverloadIndex >= 0).ShouldBeTrue();
        (existingOverloadIndex >= 0).ShouldBeTrue();
        (renamedOverloadIndex < existingOverloadIndex).ShouldBeTrue();
        (existingOverloadIndex < executeIndex).ShouldBeTrue();
    }

    [Fact]
    public async Task Async_suffix_fix_orders_overloads_by_signature_shape()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(string route, CancellationToken ct)
                {
                    return Task.FromResult(route);
                }

                public Task<string> Load()
                {
                    return Task.FromResult("default");
                }

                public async Task<string> LoadAsync(CancellationToken ct)
                {
                    await Task.Yield();
                    return "VT-42";
                }

                public Task<string> Execute(CancellationToken ct)
                {
                    return LoadAsync(ct);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.AsyncSuffix, "LoadAsync(CancellationToken ct)");

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        var loadWithoutParametersIndex = updatedText.IndexOf("public Task<string> Load()", StringComparison.Ordinal);
        var loadWithCancellationTokenIndex = updatedText.IndexOf("public async Task<string> Load(CancellationToken ct)", StringComparison.Ordinal);
        var loadWithTwoParametersIndex = updatedText.IndexOf("public Task<string> Load(string route, CancellationToken ct)", StringComparison.Ordinal);
        (updatedText).ShouldContain("Load(string route, CancellationToken ct)", StringComparison.Ordinal);
        (updatedText).ShouldContain("Load(CancellationToken ct)", StringComparison.Ordinal);
        (updatedText).ShouldContain("return Load(ct);", StringComparison.Ordinal);
        (loadWithoutParametersIndex >= 0).ShouldBeTrue();
        (loadWithCancellationTokenIndex >= 0).ShouldBeTrue();
        (loadWithTwoParametersIndex >= 0).ShouldBeTrue();
        (loadWithoutParametersIndex < loadWithCancellationTokenIndex).ShouldBeTrue();
        (loadWithCancellationTokenIndex < loadWithTwoParametersIndex).ShouldBeTrue();
    }

    [Fact]
    public async Task Async_suffix_fix_is_not_offered_for_override_methods()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public abstract class BaseReader : StringReader
            {
            }

            public sealed class DemoReader : BaseReader
            {
                public override Task<string?> ReadLineAsync()
                {
                    return Task.FromResult<string?>(string.Empty);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.AsyncSuffix, "ReadLineAsync");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        (codeActions).ShouldBeEmpty();
    }

    [Fact]
    public async Task Async_suffix_fix_is_not_offered_for_interface_implementations()
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
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.AsyncSuffix, "DisposeAsync");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        (codeActions).ShouldBeEmpty();
    }

    [Fact]
    public async Task CancellationToken_name_fix_renames_parameter_and_references()
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
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.CancellationTokenParameterName, "CancellationToken cancellationToken");

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        (updatedText).ShouldContain("Load(CancellationToken ct)", StringComparison.Ordinal);
        (updatedText).ShouldContain("ct.ThrowIfCancellationRequested();", StringComparison.Ordinal);
        (updatedText).ShouldNotContain("cancellationToken", StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationToken_default_value_fix_removes_default_literal()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(CancellationToken ct = default)
                {
                    return Task.FromResult("VT-42");
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.CancellationTokenDefaultValue, "CancellationToken ct = default");

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        (updatedText).ShouldContain("Load(CancellationToken ct)", StringComparison.Ordinal);
        (updatedText).ShouldNotContain("ct = default", StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationToken_default_value_fix_removes_default_expression()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(CancellationToken ct = default(CancellationToken))
                {
                    return Task.FromResult("VT-42");
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.CancellationTokenDefaultValue, "CancellationToken ct = default(CancellationToken)");

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        (updatedText).ShouldContain("Load(CancellationToken ct)", StringComparison.Ordinal);
        (updatedText).ShouldNotContain("default(CancellationToken)", StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationToken_default_value_fix_removes_interface_default_literal()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public interface ITourLoader
            {
                Task<string> Load(CancellationToken ct = default);
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.CancellationTokenDefaultValue, "CancellationToken ct = default");

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        (updatedText).ShouldContain("Load(CancellationToken ct);", StringComparison.Ordinal);
        (updatedText).ShouldNotContain("ct = default", StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationToken_default_value_fix_preserves_trailing_comments()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(CancellationToken ct /* preserved */ = default)
                {
                    return Task.FromResult("VT-42");
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.CancellationTokenDefaultValue, "CancellationToken ct /* preserved */ = default");

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        (updatedText).ShouldContain("/* preserved */", StringComparison.Ordinal);
        (updatedText).ShouldNotContain("= default", StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationToken_default_value_fix_is_not_offered_when_preceding_parameter_is_optional()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public interface ITourLoader
            {
                Task<string> Load(string? route = null, CancellationToken ct = default);
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.CancellationTokenDefaultValue, "CancellationToken ct = default");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        (codeActions).ShouldBeEmpty();
    }

    [Fact]
    public async Task CancellationToken_name_fix_is_not_offered_when_ct_already_exists()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(string ct, CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(ct);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.CancellationTokenParameterName, "CancellationToken cancellationToken");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        (codeActions).ShouldBeEmpty();
    }

    [Fact]
    public async Task CancellationToken_name_fix_is_not_offered_when_containing_method_declares_local_ct()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(CancellationToken cancellationToken)
                {
                    var ct = string.Empty;
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(ct);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.CancellationTokenParameterName, "CancellationToken cancellationToken");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        (codeActions).ShouldBeEmpty();
    }

    [Fact]
    public async Task CancellationToken_name_fix_is_not_offered_when_containing_method_declares_foreach_ct()
    {
        // Arrange
        const string source = """
            namespace Demo;

            using System.Collections.Generic;

            public sealed class TourLoader
            {
                public Task<string> Load(CancellationToken cancellationToken)
                {
                    foreach (var ct in new[] { "VT-42" })
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return Task.FromResult(ct);
                    }

                    return Task.FromResult(string.Empty);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.CancellationTokenParameterName, "CancellationToken cancellationToken");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        (codeActions).ShouldBeEmpty();
    }

    [Fact]
    public async Task CancellationToken_name_fix_is_not_offered_when_containing_method_declares_local_function_ct()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(CancellationToken cancellationToken)
                {
                    Task<string> ct() => Task.FromResult("VT-42");
                    cancellationToken.ThrowIfCancellationRequested();
                    return ct();
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.CancellationTokenParameterName, "CancellationToken cancellationToken");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        (codeActions).ShouldBeEmpty();
    }

    [Fact]
    public async Task CancellationToken_name_fix_ignores_ct_declared_inside_nested_lambda()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(CancellationToken cancellationToken)
                {
                    System.Func<string> nested = () =>
                    {
                        var ct = "nested";
                        return ct;
                    };

                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(nested());
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.CancellationTokenParameterName, "CancellationToken cancellationToken");

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        (updatedText).ShouldContain("Load(CancellationToken ct)", StringComparison.Ordinal);
        (updatedText).ShouldContain("var ct = \"nested\";", StringComparison.Ordinal);
        (updatedText).ShouldContain("ct.ThrowIfCancellationRequested();", StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationToken_name_fix_is_not_offered_when_simple_lambda_body_declares_ct()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public System.Func<CancellationToken, string> Build()
                {
                    return cancellationToken =>
                    {
                        var ct = "lambda";
                        cancellationToken.ThrowIfCancellationRequested();
                        return ct;
                    };
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.CancellationTokenParameterName, "cancellationToken");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        (codeActions).ShouldBeEmpty();
    }

    [Fact]
    public async Task CancellationToken_name_fix_is_not_offered_when_containing_method_declares_catch_ct()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(CancellationToken cancellationToken)
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    catch (System.Exception ct)
                    {
                        return Task.FromResult(ct.Message);
                    }

                    return Task.FromResult("VT-42");
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.CancellationTokenParameterName, "CancellationToken cancellationToken");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        (codeActions).ShouldBeEmpty();
    }

    [Fact]
    public async Task CancellationToken_name_fix_is_not_offered_when_containing_method_declares_pattern_ct()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(CancellationToken cancellationToken)
                {
                    object message = "VT-42";
                    if (message is string ct)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return Task.FromResult(ct);
                    }

                    return Task.FromResult(string.Empty);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.CancellationTokenParameterName, "CancellationToken cancellationToken");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        (codeActions).ShouldBeEmpty();
    }

    [Fact]
    public void Fixable_diagnostic_ids_match_registered_style_fixes()
    {
        // Arrange
        CodeFixProvider provider = new SharedKernelStyleCodeFixProvider();

        // Act
        var diagnosticIds = provider.FixableDiagnosticIds.ToArray();

        // Assert
        (diagnosticIds).ShouldBe([
                Analyzers.StyleDiagnosticIds.AsyncSuffix,
                Analyzers.StyleDiagnosticIds.CancellationTokenParameterName,
                Analyzers.StyleDiagnosticIds.CancellationTokenDefaultValue,
                Analyzers.StyleDiagnosticIds.GenericTypeNameSuffix,
                Analyzers.StyleDiagnosticIds.BroadOperationCanceledExceptionFilter,
                Analyzers.StyleDiagnosticIds.DomainEventSuffix
            ]);
    }

    [Fact]
    public async Task Generic_type_suffix_fix_renames_type_and_references()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Result
            {
            }

            public sealed class ResultOfT<T>
            {
            }

            public sealed class Consumer
            {
                public ResultOfT<string> Load() => new ResultOfT<string>();
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.GenericTypeNameSuffix, "ResultOfT<T>");

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        updatedText.ShouldContain("public sealed class Result<T>", StringComparison.Ordinal);
        updatedText.ShouldContain("public Result<string> Load() => new Result<string>();", StringComparison.Ordinal);
        updatedText.ShouldNotContain("ResultOfT", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generic_type_suffix_fix_is_not_offered_when_same_arity_type_exists()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Result<T>
            {
            }

            public sealed class ResultGeneric<T>
            {
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.GenericTypeNameSuffix, "ResultGeneric<T>");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        codeActions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Generic_type_suffix_fix_is_not_offered_for_nested_type()
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
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.GenericTypeNameSuffix, "ResultOfT<T>");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        codeActions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Generic_delegate_suffix_fix_renames_delegate_and_references()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public delegate void ResultHandlerOfT<T>(T value);

            public sealed class Consumer
            {
                public ResultHandlerOfT<string> Handler { get; } = static _ => { };
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.GenericTypeNameSuffix, "ResultHandlerOfT<T>");

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        updatedText.ShouldContain("public delegate void ResultHandler<T>(T value);", StringComparison.Ordinal);
        updatedText.ShouldContain("public ResultHandler<string> Handler", StringComparison.Ordinal);
        updatedText.ShouldNotContain("ResultHandlerOfT", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Domain_event_suffix_fix_renames_type_and_references()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Domain
            {
                public interface IDomainEvent;
            }

            namespace Demo;

            public sealed record TourCreated(System.Guid TourId) : SharedKernel.Domain.IDomainEvent;

            public sealed class Consumer
            {
                public TourCreated Create() => new(System.Guid.NewGuid());
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var analyzerDiagnostics = await workspace.GetAnalyzerDiagnostics(new styleanalyzers::SharedKernel.Style.Analyzers.SharedKernelStyleAnalyzer());
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = analyzerDiagnostics.ShouldHaveSingleItem(static candidate => candidate.Id == Analyzers.StyleDiagnosticIds.DomainEventSuffix);

        diagnostic.Location.SourceSpan.Length.ShouldBe("TourCreated".Length);

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        updatedText.ShouldContain("public sealed record TourCreatedDomainEvent(System.Guid TourId) : SharedKernel.Domain.IDomainEvent;", StringComparison.Ordinal);
        updatedText.ShouldContain("public TourCreatedDomainEvent Create() => new(System.Guid.NewGuid());", StringComparison.Ordinal);
        updatedText.ShouldNotContain("TourCreated ", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Domain_event_suffix_fix_is_not_offered_when_target_type_exists()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed record TourCreated(System.Guid TourId);

            public sealed record TourCreatedDomainEvent(System.Guid TourId);
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.DomainEventSuffix, "TourCreated(System.Guid TourId)");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        codeActions.ShouldBeEmpty();
    }

    [Fact]
    public void Fix_all_is_advertised_for_safe_style_diagnostics()
    {
        // Arrange
        var provider = new SharedKernelStyleCodeFixProvider();

        // Act
        var supportedDiagnosticIds = provider.GetFixAllProvider()
            .GetSupportedFixAllDiagnosticIds(provider)
            .ToArray();

        // Assert
        (supportedDiagnosticIds).ShouldBe([
                Analyzers.StyleDiagnosticIds.AsyncSuffix
            ]);
    }

    [Fact]
    public void Fix_all_provider_throws_when_original_provider_is_null()
    {
        // Arrange
        var fixAllProvider = new SharedKernelStyleCodeFixProvider().GetFixAllProvider();

        // Assert
        var exception = ((Func<object?>)(() => fixAllProvider.GetSupportedFixAllDiagnosticIds(null!))).ShouldThrow<ArgumentNullException>();
        (exception.ParamName).ShouldBe("originalCodeFixProvider");
    }

    [Fact]
    public void Fix_all_provider_exposes_the_batch_fixer_scopes()
    {
        // Arrange
        var fixAllProvider = new SharedKernelStyleCodeFixProvider().GetFixAllProvider();

        // Act
        var supportedScopes = fixAllProvider.GetSupportedFixAllScopes()
            .OrderBy(static scope => scope)
            .ToArray();
        var expectedScopes = WellKnownFixAllProviders.BatchFixer
            .GetSupportedFixAllScopes()
            .OrderBy(static scope => scope)
            .ToArray();

        // Assert
        (supportedScopes).ShouldBe(expectedScopes);
    }

    [Fact]
    public async Task Organizer_returns_original_solution_when_document_is_missing()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> LoadAsync(CancellationToken ct) => Task.FromResult(string.Empty);
            }
            """;
        using var workspace = new AdhocWorkspace();
        var project = SharedKernelStyleCodeFixProviderTestsHelpers.CreateProject(workspace, source, out var documentId);
        var document = (project.GetDocument(documentId)).ShouldBeOfType<Document>();
        var root = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        _ = (root).ShouldNotBeNull();
        var targetMethod = (root.DescendantNodes().OfType<MethodDeclarationSyntax>()).ShouldHaveSingleItem();

        // Act
        var organizedSolution = await SharedKernelStyleCodeFixProviderTestsHelpers.OrganizeOverloads(
            workspace.CurrentSolution,
            DocumentId.CreateNewId(project.Id),
            targetMethod,
            updatedName: "Load",
            TestContext.Current.CancellationToken);

        // Assert
        (organizedSolution).ShouldBeSameAs(workspace.CurrentSolution);
    }

    [Fact]
    public async Task Organizer_returns_original_solution_when_target_method_is_not_found_in_document()
    {
        // Arrange
        using var workspace = new AdhocWorkspace();
        var sourceProject = SharedKernelStyleCodeFixProviderTestsHelpers.CreateProject(
            workspace,
            """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> LoadAsync(CancellationToken ct) => Task.FromResult(string.Empty);
            }
            """,
            out var sourceDocumentId);
        var otherProject = SharedKernelStyleCodeFixProviderTestsHelpers.CreateProject(
            workspace,
            """
            namespace Demo;

            public sealed class OtherLoader
            {
                public Task<string> Execute(CancellationToken ct) => Task.FromResult(string.Empty);
            }
            """,
            out var otherDocumentId,
            assemblyName: "SharedKernel.Style.CodeFixes.Tests.Other");
        var sourceDocument = (sourceProject.GetDocument(sourceDocumentId)).ShouldBeOfType<Document>();
        var sourceRoot = await sourceDocument.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        _ = (sourceRoot).ShouldNotBeNull();
        var targetMethod = (sourceRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()).ShouldHaveSingleItem();

        // Act
        var organizedSolution = await SharedKernelStyleCodeFixProviderTestsHelpers.OrganizeOverloads(
            workspace.CurrentSolution,
            otherDocumentId,
            targetMethod,
            updatedName: "Load",
            TestContext.Current.CancellationToken);

        // Assert
        (organizedSolution).ShouldBeSameAs(workspace.CurrentSolution);
        (otherProject.GetDocument(otherDocumentId)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Organizer_returns_original_solution_when_target_method_syntaxtree_differs_from_document()
    {
        // Arrange
        using var workspace = new AdhocWorkspace();
        var project = SharedKernelStyleCodeFixProviderTestsHelpers.CreateProject(
            workspace,
            """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> LoadAsync(CancellationToken ct) => Task.FromResult(string.Empty);
            }
            """,
            out var documentId,
            assemblyName: "SharedKernel.Style.CodeFixes.Tests.SyntaxTreeMismatch");
        var document = (project.GetDocument(documentId)).ShouldBeOfType<Document>();
        var detachedTree = CSharpSyntaxTree.ParseText(
            """
            namespace Demo;

            public sealed class DetachedLoader
            {
                public Task<string> LoadAsync(CancellationToken ct) => Task.FromResult(string.Empty);
            }
            """,
            new CSharpParseOptions(LanguageVersion.Preview),
            cancellationToken: TestContext.Current.CancellationToken);
        var detachedRoot = await detachedTree.GetRootAsync(TestContext.Current.CancellationToken);
        var detachedMethod = (detachedRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()).ShouldHaveSingleItem();

        // Act
        var organizedSolution = await SharedKernelStyleCodeFixProviderTestsHelpers.OrganizeOverloads(
            workspace.CurrentSolution,
            documentId,
            detachedMethod,
            updatedName: "Load",
            TestContext.Current.CancellationToken);

        // Assert
        (organizedSolution).ShouldBeSameAs(workspace.CurrentSolution);
        _ = (document).ShouldNotBeNull();
    }

    [Fact]
    public async Task Organizer_orders_overloads_with_params_modifier_before_non_params()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(string[] values) => Task.FromResult(string.Empty);
                public Task<string> Execute(CancellationToken ct) => Task.FromResult(string.Empty);
                public Task<string> Load(params string[] values) => Task.FromResult(string.Empty);
            }
            """;
        using var workspace = new AdhocWorkspace();
        var project = SharedKernelStyleCodeFixProviderTestsHelpers.CreateProject(workspace, source, out var documentId, assemblyName: "SharedKernel.Style.CodeFixes.Tests.Params");
        var document = (project.GetDocument(documentId)).ShouldBeOfType<Document>();
        var root = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        _ = (root).ShouldNotBeNull();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
        var targetMethod = methods.Single(method => method.Identifier.ValueText == "Load" && method.ParameterList.Parameters[0].Modifiers.Count > 0);

        // Act
        var organizedSolution = await SharedKernelStyleCodeFixProviderTestsHelpers.OrganizeOverloads(
            workspace.CurrentSolution,
            documentId,
            targetMethod,
            updatedName: "Load",
            TestContext.Current.CancellationToken);
        var updatedText = await SharedKernelStyleCodeFixProviderTestsHelpers.ReadDocumentText(organizedSolution, documentId);

        // Assert
        var paramsIndex = updatedText.IndexOf("Load(params string[] values)", StringComparison.Ordinal);
        var regularIndex = updatedText.IndexOf("Load(string[] values)", StringComparison.Ordinal);
        (paramsIndex >= 0).ShouldBeTrue();
        (regularIndex >= 0).ShouldBeTrue();
        (paramsIndex < regularIndex).ShouldBeTrue();
    }

    [Fact]
    public async Task Renamed_method_match_returns_false_when_original_symbol_is_not_a_method()
    {
        // Arrange
        using var workspace = new AdhocWorkspace();
        var project = SharedKernelStyleCodeFixProviderTestsHelpers.CreateProject(
            workspace,
            """
            namespace Demo;

            public sealed class TourLoader
            {
                public int Value { get; } = 42;

                public Task<string> Load(CancellationToken ct) => Task.FromResult(string.Empty);
            }
            """,
            out var documentId,
            assemblyName: "SharedKernel.Style.CodeFixes.Tests.SymbolMatch");
        var document = (project.GetDocument(documentId)).ShouldBeOfType<Document>();
        var semanticModel = await document.GetSemanticModelAsync(TestContext.Current.CancellationToken);
        _ = (semanticModel).ShouldNotBeNull();
        var root = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        _ = (root).ShouldNotBeNull();
        var candidateMethod = (root.DescendantNodes().OfType<MethodDeclarationSyntax>()).ShouldHaveSingleItem();
        var candidateMethodSymbol = semanticModel.GetDeclaredSymbol(candidateMethod, TestContext.Current.CancellationToken);
        _ = (candidateMethodSymbol).ShouldNotBeNull();
        var propertyDeclaration = (root.DescendantNodes().OfType<PropertyDeclarationSyntax>()).ShouldHaveSingleItem();
        var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDeclaration, TestContext.Current.CancellationToken);
        _ = (propertySymbol).ShouldNotBeNull();

        // Act
        var isMatch = SharedKernelStyleCodeFixProviderTestsHelpers.InvokeIsRenamedMethodMatch(
            candidateMethodSymbol,
            propertySymbol,
            updatedName: "Load");

        // Assert
        (isMatch).ShouldBeFalse();
    }

    [Fact]
    public async Task Organizer_orders_overloads_by_ref_kind_when_parameter_count_and_type_match()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(out int value) { value = 0; return Task.FromResult(string.Empty); }
                public Task<string> Execute(CancellationToken ct) => Task.FromResult(string.Empty);
                public Task<string> Load(ref int value) => Task.FromResult(string.Empty);
                public Task<string> Load(int value) => Task.FromResult(string.Empty);
                public Task<string> Load(in int value) => Task.FromResult(string.Empty);
            }
            """;
        using var workspace = new AdhocWorkspace();
        var project = SharedKernelStyleCodeFixProviderTestsHelpers.CreateProject(workspace, source, out var documentId, assemblyName: "SharedKernel.Style.CodeFixes.Tests.RefKinds");
        var document = (project.GetDocument(documentId)).ShouldBeOfType<Document>();
        var root = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        _ = (root).ShouldNotBeNull();
        var targetMethod = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();

        // Act
        var organizedSolution = await SharedKernelStyleCodeFixProviderTestsHelpers.OrganizeOverloads(
            workspace.CurrentSolution,
            documentId,
            targetMethod,
            updatedName: "Load",
            TestContext.Current.CancellationToken);
        var updatedText = await SharedKernelStyleCodeFixProviderTestsHelpers.ReadDocumentText(organizedSolution, documentId);

        // Assert
        var valueIndex = updatedText.IndexOf("Load(int value)", StringComparison.Ordinal);
        var refIndex = updatedText.IndexOf("Load(ref int value)", StringComparison.Ordinal);
        var outIndex = updatedText.IndexOf("Load(out int value)", StringComparison.Ordinal);
        var inIndex = updatedText.IndexOf("Load(in int value)", StringComparison.Ordinal);
        (valueIndex >= 0).ShouldBeTrue();
        (refIndex >= 0).ShouldBeTrue();
        (outIndex >= 0).ShouldBeTrue();
        (inIndex >= 0).ShouldBeTrue();
        (valueIndex < refIndex).ShouldBeTrue();
        (refIndex < outIndex).ShouldBeTrue();
        (outIndex < inIndex).ShouldBeTrue();
    }

    [Fact]
    public async Task Organizer_orders_generic_overloads_after_non_generic_overloads_with_same_parameter_count()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load<T>(T value) => Task.FromResult(string.Empty);
                public Task<string> Execute(CancellationToken ct) => Task.FromResult(string.Empty);
                public Task<string> Load(string value) => Task.FromResult(string.Empty);
            }
            """;
        using var workspace = new AdhocWorkspace();
        var project = SharedKernelStyleCodeFixProviderTestsHelpers.CreateProject(workspace, source, out var documentId, assemblyName: "SharedKernel.Style.CodeFixes.Tests.Generic");
        var document = (project.GetDocument(documentId)).ShouldBeOfType<Document>();
        var root = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        _ = (root).ShouldNotBeNull();
        var targetMethod = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();

        // Act
        var organizedSolution = await SharedKernelStyleCodeFixProviderTestsHelpers.OrganizeOverloads(
            workspace.CurrentSolution,
            documentId,
            targetMethod,
            updatedName: "Load",
            TestContext.Current.CancellationToken);
        var updatedText = await SharedKernelStyleCodeFixProviderTestsHelpers.ReadDocumentText(organizedSolution, documentId);

        // Assert
        var nonGenericIndex = updatedText.IndexOf("Load(string value)", StringComparison.Ordinal);
        var genericIndex = updatedText.IndexOf("Load<T>(T value)", StringComparison.Ordinal);
        (nonGenericIndex >= 0).ShouldBeTrue();
        (genericIndex >= 0).ShouldBeTrue();
        (nonGenericIndex < genericIndex).ShouldBeTrue();
    }

    [Fact]
    public async Task Broad_operation_cancelled_exception_filter_fix_uses_shared_helper()
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
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            Analyzers.StyleDiagnosticIds.BroadOperationCanceledExceptionFilter,
            "ex is not OperationCanceledException");

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        (updatedText).ShouldContain("using SharedKernel.BuildingBlocks;", StringComparison.Ordinal);
        (updatedText).ShouldContain("catch (Exception ex) when (ex.ShouldHandleAsFailure(ct))", StringComparison.Ordinal);
        (updatedText).ShouldNotContain("ex is not OperationCanceledException", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Broad_operation_cancelled_exception_filter_fix_does_not_duplicate_existing_using()
    {
        // Arrange
        const string source = """
            using SharedKernel.BuildingBlocks;

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
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            Analyzers.StyleDiagnosticIds.BroadOperationCanceledExceptionFilter,
            "ex is not OperationCanceledException");

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        (updatedText.LastIndexOf("using SharedKernel.BuildingBlocks;", StringComparison.Ordinal)).ShouldBe(updatedText.IndexOf("using SharedKernel.BuildingBlocks;", StringComparison.Ordinal));
        (updatedText).ShouldContain("catch (Exception ex) when (ex.ShouldHandleAsFailure(ct))", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Broad_operation_cancelled_exception_filter_without_ct_has_no_code_fix()
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
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            Analyzers.StyleDiagnosticIds.BroadOperationCanceledExceptionFilter,
            "ex is not OperationCanceledException");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        (codeActions).ShouldBeEmpty();
    }

}
