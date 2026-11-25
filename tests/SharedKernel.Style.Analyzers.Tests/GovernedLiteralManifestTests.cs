using PandaBear.Testing;
using PandaBear.Testing.Roslyn;

namespace PandaBear.Style.Analyzers.Tests;

/// <summary>Tests reconstruction of the compilations that the constant guard inspects.</summary>
public sealed class GovernedLiteralManifestTests
{
    [Fact]
    public void Compile_uses_the_recorded_assembly_sources_references_and_defines()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var sourcePath = Path.Combine(directory.Path, "Keys.cs");
        var manifestPath = Path.Combine(directory.Path, "test.inputs");
        File.WriteAllText(sourcePath,
            """
            #if ENABLE_VALUE
            namespace Demo;
            public static class Keys
            {
                public const string Value = "registered";
            }
            #endif
            """);
        File.WriteAllLines(manifestPath,
        [
            "assembly|Production",
            "defines|ENABLE_VALUE",
            $"source|{sourcePath}",
            $"reference|{typeof(string).Assembly.Location}",
        ]);

        // Act
        var compilation = GovernedLiteralManifest.Compile(manifestPath);
        var errors = GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value");

        // Assert
        errors.ShouldBeEmpty();
    }
}
