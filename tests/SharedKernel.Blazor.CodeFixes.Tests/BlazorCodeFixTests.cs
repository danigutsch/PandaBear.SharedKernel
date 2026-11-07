using Microsoft.CodeAnalysis;
using PandaBear.Blazor.Analyzers;
using PandaBear.CodeFixes.Testing;

namespace PandaBear.Blazor.CodeFixes.Tests;

/// <summary>Tests the code fixes for the Blazor component analyzers.</summary>
public sealed class BlazorCodeFixTests
{
    private const string FlagSource = """
        using Microsoft.AspNetCore.Components;

        namespace Demo;

        public sealed class Page : ComponentBase
        {
            private bool _saving;

            private Task Save() => Task.CompletedTask;
            private Task<int> Load() => Task.FromResult(1);
            private void Use(int value) { }
            private Task<bool> TryGet(out int item) { item = 1; return Task.FromResult(true); }
            private Task<(int, int)> Pair() => Task.FromResult((1, 2));

            public async Task Submit()
            {
                _saving = true;
                await Save();
                _saving = false;
            }
        }
        """;

    [Fact]
    public async Task The_busy_flag_fix_moves_the_reset_into_a_finally_block()
    {
        // Arrange
        var workspace = CodeFixTestWorkspace.Create(FlagSource);
        var diagnostic = await workspace.CreateDocumentDiagnostic(BlazorDiagnosticIds.UnguardedBusyFlag, "_saving = true");

        // Act
        var actions = await workspace.GetCodeActions(new SharedKernelBlazorCodeFixProvider(), diagnostic);
        var action = actions.ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(action);
        var text = await workspace.GetDocumentText();
        var errors = (await workspace.GetCompilationDiagnostics()).Where(static candidate => candidate.Severity == DiagnosticSeverity.Error).ToArray();

        // Assert
        action.EquivalenceKey.ShouldBe("ResetBusyFlagInFinally");
        text.WithoutWhitespace().ShouldContain("_saving=true;try{awaitSave();}finally{_saving=false;}", StringComparison.Ordinal);
        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_busy_flag_fix_wraps_an_await_foreach_with_a_deconstruction()
    {
        // Arrange
        var source = FlagSource
            .Replace("await Save();", "await foreach (var (first, second) in Pairs()) { Use(first + second); }", StringComparison.Ordinal)
            .Replace(
                "private Task<(int, int)> Pair()",
                "private async System.Collections.Generic.IAsyncEnumerable<(int, int)> Pairs() { await Task.CompletedTask; yield return (1, 2); }\n    private Task<(int, int)> Pair()",
                StringComparison.Ordinal);
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(BlazorDiagnosticIds.UnguardedBusyFlag, "_saving = true");

        // Act
        var actions = await workspace.GetCodeActions(new SharedKernelBlazorCodeFixProvider(), diagnostic);
        await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        var text = await workspace.GetDocumentText();

        // Assert
        text.WithoutWhitespace().ShouldContain("_saving=true;try{awaitforeach(var(first,second)inPairs()){Use(first+second);}}finally{_saving=false;}", StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_busy_flag_fix_is_not_offered_when_a_local_is_used_after_the_reset()
    {
        // Arrange
        var source = FlagSource.Replace(
            "await Save();\n        _saving = false;",
            "var value = await Load();\n        _saving = false;\n        Use(value);",
            StringComparison.Ordinal);
        source.ShouldContain("Use(value);", StringComparison.Ordinal);
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(BlazorDiagnosticIds.UnguardedBusyFlag, "_saving = true");

        // Act
        var actions = await workspace.GetCodeActions(new SharedKernelBlazorCodeFixProvider(), diagnostic);

        // Assert
        actions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("await TryGet(out var item);", "Use(item);")]
    [InlineData("var ok = await Load() is int found;", "Use(found);")]
    [InlineData("var (first, second) = await Pair();", "Use(first);")]
    public async Task The_busy_flag_fix_is_not_offered_when_an_expression_variable_is_used_after_the_reset(string guarded, string after)
    {
        // Arrange
        var source = FlagSource.Replace(
            "await Save();\n        _saving = false;",
            $"{guarded}\n        _saving = false;\n        {after}",
            StringComparison.Ordinal);
        source.ShouldContain(after, StringComparison.Ordinal);
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(BlazorDiagnosticIds.UnguardedBusyFlag, "_saving = true");

        // Act
        var actions = await workspace.GetCodeActions(new SharedKernelBlazorCodeFixProvider(), diagnostic);

        // Assert
        actions.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_busy_flag_fix_is_offered_when_an_out_variable_is_not_used_after_the_reset()
    {
        // Arrange
        var source = FlagSource.Replace("await Save();", "await TryGet(out var item);\n        Use(item);", StringComparison.Ordinal);
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(BlazorDiagnosticIds.UnguardedBusyFlag, "_saving = true");

        // Act
        var actions = await workspace.GetCodeActions(new SharedKernelBlazorCodeFixProvider(), diagnostic);
        var action = actions.ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(action);
        var errors = (await workspace.GetCompilationDiagnostics()).Where(static candidate => candidate.Severity == DiagnosticSeverity.Error).ToArray();

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_sender_fix_replaces_the_injected_type_with_the_scoped_sender()
    {
        // Arrange
        const string source = """
            using Microsoft.AspNetCore.Components;
            using PandaBear.Mediator;

            namespace Demo;

            public sealed class Page : ComponentBase
            {
                [Inject]
                public ISender Sender { get; set; } = default!;
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(BlazorDiagnosticIds.LongLivedScopeService, "ISender");

        // Act
        var actions = await workspace.GetCodeActions(new SharedKernelBlazorCodeFixProvider(), diagnostic);
        var action = actions.ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(action);
        var text = await workspace.GetDocumentText();
        var errors = (await workspace.GetCompilationDiagnostics()).Where(static candidate => candidate.Severity == DiagnosticSeverity.Error).ToArray();

        // Assert
        action.EquivalenceKey.ShouldBe("UseScopedSender");
        text.ShouldContain("public ScopedSender Sender { get; set; } = default!;", StringComparison.Ordinal);
        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task No_fix_is_offered_for_an_injected_db_context()
    {
        // Arrange
        const string source = """
            using Microsoft.AspNetCore.Components;

            namespace Demo;

            public sealed class ShopDb : Microsoft.EntityFrameworkCore.DbContext;

            public sealed class Page : ComponentBase
            {
                [Inject]
                private ShopDb Db { get; set; } = default!;
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(BlazorDiagnosticIds.LongLivedScopeService, "ShopDb Db");

        // Act
        var actions = await workspace.GetCodeActions(new SharedKernelBlazorCodeFixProvider(), diagnostic);

        // Assert
        actions.ShouldBeEmpty();
    }
}
