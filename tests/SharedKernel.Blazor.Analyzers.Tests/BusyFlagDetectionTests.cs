namespace PandaBear.Blazor.Analyzers.Tests;

/// <summary>Tests which awaits and catch-all shapes the busy-flag analyzer (SKBLZ001) counts as unprotected awaits.</summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ComponentStateCapability)]
public sealed class BusyFlagDetectionTests
{
    [Theory]
    [InlineData("async Task Inner() { await Save(); }")]
    [InlineData("Func<Task> inner = async () => { await Save(); };")]
    [InlineData("Func<Task> inner = async delegate { await Save(); };")]
    [InlineData("Func<Task> inner = () => { async Task Nested() { await Save(); } return Nested(); };")]
    public async Task An_await_inside_a_nested_function_between_set_and_reset_is_not_reported(string statement)
    {
        // Arrange
        var source = BusyFlagSources.Page($"_saving = true;\n{statement}\n_saving = false;");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("await foreach (var x in Numbers()) { Log(); }")]
    [InlineData("await foreach (var (a, b) in Pairs()) { Log(); }")]
    [InlineData("await foreach ((int a, int b) in Pairs()) { Log(); }")]
    [InlineData("await using (var resource = new Resource()) { Log(); }")]
    [InlineData("await using var resource = new Resource();")]
    public async Task An_await_statement_between_set_and_reset_is_reported(string statement)
    {
        // Arrange
        var source = BusyFlagSources.Page($"_saving = true;\n{statement}\n_saving = false;");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe(BlazorDiagnosticIds.UnguardedBusyFlag);
    }

    [Theory]
    [InlineData("return;")]
    [InlineData("throw new InvalidOperationException();")]
    [InlineData("_ = Name() ?? throw new InvalidOperationException();")]
    [InlineData("Log(); if (_saving) { return; }")]
    [InlineData("goto done;")]
    public async Task A_catch_all_that_leaves_the_method_does_not_protect_the_await(string catchBody)
    {
        // Arrange
        var source = BusyFlagSources.Page(
            $"_saving = true;\ntry\n{{\n    await Save();\n}}\ncatch\n{{\n    {catchBody}\n}}\n_saving = false;\ndone:\nLog();");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe(BlazorDiagnosticIds.UnguardedBusyFlag);
    }

    [Theory]
    [InlineData("break;")]
    [InlineData("continue;")]
    public async Task A_catch_all_that_leaves_the_loop_does_not_protect_the_await(string jump)
    {
        // Arrange
        var source = BusyFlagSources.Page(
            $"while (true)\n{{\n    _saving = true;\n    try\n    {{\n        await Save();\n    }}\n    catch\n    {{\n        {jump}\n    }}\n    _saving = false;\n}}");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe(BlazorDiagnosticIds.UnguardedBusyFlag);
    }

    [Theory]
    [InlineData("Action leave = () => { return; };\nleave();")]
    [InlineData("foreach (var item in new[] { 1, 2 }) { if (item > 1) { break; } else { continue; } }")]
    [InlineData("Action leave = () => throw new InvalidOperationException();")]
    [InlineData("while (_saving) { Log(); break; }")]
    public async Task A_catch_all_whose_jumps_stay_inside_the_catch_still_protects_the_await(string catchBody)
    {
        // Arrange
        var source = BusyFlagSources.Page(
            $"_saving = true;\ntry\n{{\n    await Save();\n}}\ncatch\n{{\n    {catchBody}\n}}\n_saving = false;");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
