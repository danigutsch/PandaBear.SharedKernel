namespace PandaBear.Style.CodeFixes.Tests;

public sealed class CurrentTimeCodeFixTests
{
    [Theory]
    [InlineData("DateTimeOffset.UtcNow", "TimeProvider clock", "clock.GetUtcNow()")]
    [InlineData("DateTime.UtcNow", "TimeProvider clock", "clock.GetUtcNow().UtcDateTime")]
    [InlineData("DateTimeOffset.Now", "TimeProvider clock", "clock.GetLocalNow()")]
    public async Task Fix_uses_the_unique_in_scope_provider(string expression, string parameter, string replacement)
    {
        var workspace = CodeFixTestWorkspace.Create($$"""
            public class Reader { public object Read({{parameter}}) => {{expression}}; }
            """);
        var diagnostic = await workspace.CreateDocumentDiagnostic("SKSTYLE014", expression);
        var actions = await workspace.GetCodeActions(new SharedKernelStyleCodeFixProvider(), diagnostic);
        await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        var changed = await workspace.GetDocumentText();
        changed.ShouldContain(replacement, StringComparison.Ordinal);
        var diagnostics = await workspace.GetCompilationDiagnostics();
        diagnostics.Where(static d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("", "DateTimeOffset.UtcNow")]
    [InlineData("TimeProvider first, TimeProvider second", "DateTime.UtcNow")]
    [InlineData("TimeProvider clock", "DateTime.Now")]
    [InlineData("TimeProvider clock", "DateTime.Today")]
    public async Task Fix_declines_missing_ambiguous_or_wall_clock_conversions(string parameters, string expression)
    {
        var workspace = CodeFixTestWorkspace.Create($$"""
            public class Reader { public object Read({{parameters}}) => {{expression}}; }
            """);
        var diagnostic = await workspace.CreateDocumentDiagnostic("SKSTYLE014", expression);
        var actions = await workspace.GetCodeActions(new SharedKernelStyleCodeFixProvider(), diagnostic);
        actions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("private readonly TimeProvider _clock = TimeProvider.System;", "", "_clock.GetUtcNow()", true)]
    [InlineData("private TimeProvider Clock => TimeProvider.System;", "", "Clock.GetUtcNow()", true)]
    [InlineData("", "TimeProvider clock = TimeProvider.System;", "clock.GetUtcNow()", true)]
    [InlineData("private TimeProvider? _clock;", "", "", false)]
    [InlineData("private TimeProvider _clock = TimeProvider.System;", "", "", false, true)]
    public async Task Provider_selection_respects_members_nullability_and_static_context(string member, string local, string replacement, bool expected, bool isStatic = false)
    {
        var workspace = CodeFixTestWorkspace.Create($$"""
            #nullable enable
            public class Reader
            {
                {{member}}
                public {{(isStatic ? "static " : "")}}object Read()
                {
                    {{local}}
                    return DateTimeOffset.UtcNow;
                }
            }
            """);
        var diagnostic = await workspace.CreateDocumentDiagnostic("SKSTYLE014", "DateTimeOffset.UtcNow");
        var actions = await workspace.GetCodeActions(new SharedKernelStyleCodeFixProvider(), diagnostic);
        actions.Count.ShouldBe(expected ? 1 : 0);
        if (expected)
        {
            await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
            var changed = await workspace.GetDocumentText();
            changed.ShouldContain(replacement, StringComparison.Ordinal);
            var diagnostics = await workspace.GetCompilationDiagnostics();
            diagnostics.Where(static d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
        }
    }
}
