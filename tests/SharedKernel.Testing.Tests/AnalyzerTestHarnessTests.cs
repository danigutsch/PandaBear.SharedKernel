using System.Reflection;

namespace SharedKernel.Testing.Tests;

public sealed class AnalyzerTestHarnessTests
{
    [Fact]
    public void Applies_default_usings_and_extra_metadata_references()
    {
        // Arrange
        var source = "public sealed class Sample { [Fact] public void Runs() { } }";
        Assembly[] references = [typeof(FactAttribute).Assembly];

        // Act
        var compilation = Roslyn.AnalyzerTestHarness.CreateCompilation(
            source,
            "using Xunit;\n",
            references);
        var diagnostics = compilation.GetDiagnostics(TestContext.Current.CancellationToken);

        // Assert
        (diagnostics).ShouldBeEmpty();
    }
}
