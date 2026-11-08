namespace PandaBear.Blazor.Analyzers.Tests;

/// <summary>Tests that the analyzer reports a component flag that is not reset in a finally block (SKBLZ001) only where intended.</summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ComponentStateCapability)]
public sealed class BusyFlagAnalyzerTests
{
    [Fact]
    public void Supported_diagnostics_match_the_descriptor_contract()
    {
        // Arrange
        var analyzer = new SharedKernelBlazorAnalyzer();

        // Act
        var ids = analyzer.SupportedDiagnostics.Select(static descriptor => descriptor.Id).ToArray();

        // Assert
        ids.ShouldBe([BlazorDiagnosticIds.UnguardedBusyFlag, BlazorDiagnosticIds.LongLivedScopeService, BlazorDiagnosticIds.MissingKey, BlazorDiagnosticIds.StaleParameter]);
    }

    [Theory]
    [InlineData("private bool _saving;", "_saving")]
    [InlineData("private bool Saving { get; set; }", "Saving")]
    [InlineData("private bool _saving;", "this._saving")]
    public async Task A_flag_reset_after_an_await_without_finally_is_reported(string declaration, string flag)
    {
        // Arrange
        var source = $$"""
            namespace Demo;

            public sealed class Page : ComponentBase
            {
                {{declaration}}

                private Task Save() => Task.CompletedTask;

                public async Task Submit()
                {
                    {{flag}} = true;
                    await Save();
                    {{flag}} = false;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(BlazorDiagnosticIds.UnguardedBusyFlag);
        AnalyzerTestHarness.GetReportedLine(diagnostic).ShouldContain($"{flag} = true;", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_flag_reset_in_a_finally_block_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Page : ComponentBase
            {
                private bool _saving;

                private Task Save() => Task.CompletedTask;

                public async Task Submit()
                {
                    _saving = true;
                    try
                    {
                        await Save();
                    }
                    finally
                    {
                        _saving = false;
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_flag_reset_without_an_await_in_between_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Page : ComponentBase
            {
                private bool _open;

                public void Toggle()
                {
                    _open = true;
                    Console.WriteLine("x");
                    _open = false;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_local_variable_flag_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Page : ComponentBase
            {
                private Task Save() => Task.CompletedTask;

                public async Task Submit()
                {
                    var busy = false;
                    busy = true;
                    await Save();
                    busy = false;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_flag_in_a_class_that_is_not_a_component_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Worker
            {
                private bool _saving;

                private Task Save() => Task.CompletedTask;

                public async Task Submit()
                {
                    _saving = true;
                    await Save();
                    _saving = false;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_await_in_a_try_with_a_catch_all_that_does_not_rethrow_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Page : ComponentBase
            {
                private bool _saving;

                private Task Save() => Task.CompletedTask;
                private void Log() { }

                public async Task Submit()
                {
                    _saving = true;
                    try
                    {
                        await Save();
                    }
                    catch
                    {
                        Log();
                    }
                    _saving = false;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_await_in_a_try_with_a_catch_of_exception_that_does_not_rethrow_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Page : ComponentBase
            {
                private bool _saving;

                private Task Save() => Task.CompletedTask;
                private void Log() { }

                public async Task Submit()
                {
                    _saving = true;
                    try
                    {
                        await Save();
                    }
                    catch (Exception)
                    {
                        Log();
                    }
                    _saving = false;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_await_in_a_try_with_a_catch_of_a_specific_exception_is_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Page : ComponentBase
            {
                private bool _saving;

                private Task Save() => Task.CompletedTask;
                private void Log() { }

                public async Task Submit()
                {
                    _saving = true;
                    try
                    {
                        await Save();
                    }
                    catch (System.IO.IOException)
                    {
                        Log();
                    }
                    _saving = false;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe(BlazorDiagnosticIds.UnguardedBusyFlag);
    }

    [Fact]
    public async Task An_await_in_a_try_with_a_catch_all_that_rethrows_is_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Page : ComponentBase
            {
                private bool _saving;

                private Task Save() => Task.CompletedTask;
                private void Log() { }

                public async Task Submit()
                {
                    _saving = true;
                    try
                    {
                        await Save();
                    }
                    catch
                    {
                        Log();
                        throw;
                    }
                    _saving = false;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe(BlazorDiagnosticIds.UnguardedBusyFlag);
    }

    [Fact]
    public async Task An_await_in_a_try_with_a_filtered_catch_all_is_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Page : ComponentBase
            {
                private bool _saving;

                private Task Save() => Task.CompletedTask;
                private void Log() { }

                public async Task Submit()
                {
                    _saving = true;
                    try
                    {
                        await Save();
                    }
                    catch (Exception) when (_saving)
                    {
                        Log();
                    }
                    _saving = false;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe(BlazorDiagnosticIds.UnguardedBusyFlag);
    }

    [Fact]
    public async Task An_await_after_a_protected_try_in_the_same_span_is_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Page : ComponentBase
            {
                private bool _saving;

                private Task Save() => Task.CompletedTask;
                private void Log() { }

                public async Task Submit()
                {
                    _saving = true;
                    try
                    {
                        await Save();
                    }
                    catch
                    {
                        Log();
                    }
                    await Save();
                    _saving = false;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe(BlazorDiagnosticIds.UnguardedBusyFlag);
    }

    [Fact]
    public async Task An_await_in_the_catch_block_of_a_catch_all_is_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Page : ComponentBase
            {
                private bool _saving;

                private Task Save() => Task.CompletedTask;
                private void Log() { }

                public async Task Submit()
                {
                    _saving = true;
                    try
                    {
                        Log();
                    }
                    catch
                    {
                        await Save();
                    }
                    _saving = false;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe(BlazorDiagnosticIds.UnguardedBusyFlag);
    }
}
