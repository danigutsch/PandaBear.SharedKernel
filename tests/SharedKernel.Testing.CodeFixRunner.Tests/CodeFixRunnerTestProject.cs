using System.Globalization;

namespace PandaBear.Testing.CodeFixRunner.Tests;

/// <summary>Sample project, solution, source and configuration texts, plus helpers that write them to a temporary directory for code fix runner tests.</summary>
internal static class CodeFixRunnerTestProject
{
    /// <summary>A minimal net10.0 SDK-style project file with restore disabled.</summary>
    public const string ProjectFile = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <RestoreProjectStyle>None</RestoreProjectStyle>
          </PropertyGroup>
        </Project>
        """;

    /// <summary>A project file that is cut off before its closing tags, so MSBuild cannot load it.</summary>
    public const string MalformedProjectFile = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
        """;

    /// <summary>A <c>global.json</c> that pins SDK 10.0.999 with roll-forward disabled, a version that is not installed.</summary>
    public const string MissingSdkGlobalJsonFile = """
        {
          "sdk": {
            "version": "10.0.999",
            "rollForward": "disable"
          }
        }
        """;

    /// <summary>A <c>.slnx</c> solution that lists the existing <c>Sample.Tests.csproj</c> and a project file that does not exist.</summary>
    public const string SolutionWithMissingProjectFile = """
        <Solution>
          <Project Path="Sample.Tests.csproj" />
          <Project Path="Missing/Missing.Tests.csproj" />
        </Solution>
        """;

    /// <summary>A stub <c>Xunit.Assert</c> and a test class with one <c>Xunit.Assert.True(true)</c> call, which SKTEST006 can fix.</summary>
    public const string SourceFile = """
        namespace Xunit
        {
            public static class Assert
            {
                public static void True(bool condition)
                {
                }

                public static void Equal(string expected, string actual, bool ignoreCase)
                {
                }
            }
        }

        namespace Sample.Tests
        {
            public sealed class SampleTests
            {
                public void UsesXunitAssert()
                {
                    Xunit.Assert.True(true);
                }
            }
        }
        """;

    /// <summary>A test class with an empty method and no xUnit assertions, so no SKTEST006 diagnostic is raised.</summary>
    public const string CleanSourceFile = """
        namespace Sample.Tests
        {
            public sealed class SampleTests
            {
                public void Execute()
                {
                }
            }
        }
        """;

    /// <summary>A test method calling both <c>Xunit.Assert.Equal</c> with <c>ignoreCase</c>, which has no fix, and the fixable <c>Xunit.Assert.True</c>.</summary>
    public const string SupportedAndUnsupportedSourceFile = """
        namespace Xunit
        {
            public static class Assert
            {
                public static void True(bool condition)
                {
                }

                public static void Equal(string expected, string actual, bool ignoreCase)
                {
                }
            }
        }

        namespace Sample.Tests
        {
            public sealed class SampleTests
            {
                public void UsesXunitAssert()
                {
                    Xunit.Assert.Equal("expected", "actual", true);
                    Xunit.Assert.True(true);
                }
            }
        }
        """;

    /// <summary>A test method with two <c>Xunit.Assert.True</c> calls whose arguments carry irregular inner spacing.</summary>
    public const string MultipleSupportedSourceFile = """
        namespace Xunit
        {
            public static class Assert
            {
                public static void True(bool condition)
                {
                }
            }
        }

        namespace Sample.Tests
        {
            public sealed class SampleTests
            {
                public void UsesXunitAssert()
                {
                    Xunit.Assert.True(  true );
                    Xunit.Assert.True( true  );
                }
            }
        }
        """;

    /// <summary>A <c>[Fact]</c> test class that calls a private static <c>CreateTourId</c> helper, which SKTEST004 moves into a helpers class.</summary>
    public const string HelperSourceFile = """
        namespace Xunit
        {
            public sealed class FactAttribute : System.Attribute
            {
            }
        }

        namespace Sample.Tests
        {
            public sealed class SampleTests
            {
                [Xunit.Fact]
                public void Creates_a_sample()
                {
                    var value = CreateTourId();
                }

                private static int CreateTourId()
                {
                    return 42;
                }
            }
        }
        """;

    /// <summary>A <c>[Fact]</c> test whose Act, Arrange and Assert comment markers appear in the wrong order.</summary>
    public const string UnorderedMarkersSourceFile = """
        namespace Xunit
        {
            public sealed class FactAttribute : System.Attribute
            {
            }
        }

        namespace Sample.Tests
        {
            public sealed class SampleTests
            {
                [Xunit.Fact]
                public void CreatesSample()
                {
                    // Act
                    var value = 42;

                    // Arrange
                    var copy = value;

                    // Assert
                    _ = copy;
                }
            }
        }
        """;

    /// <summary>An <c>.editorconfig</c> that enables SKTEST017 at warning severity for C# files.</summary>
    public const string UserMessageOverloadEditorConfigFile = """
        root = true

        [*.cs]
        dotnet_diagnostic.SKTEST017.severity = warning
        """;

    /// <summary>Assertion extensions <c>ShouldBePositive</c> and <c>ShouldBeEven</c> without <c>string userMessage</c> overloads, plus the <c>WithUserMessage</c> helpers the generated overloads call.</summary>
    public const string UserMessageOverloadSourceFile = """
        using static Sample.Assertions.UserMessageAssertions;

        namespace Sample.Assertions
        {
            /// <summary>Sample assertions.</summary>
            public static class SampleAssertions
            {
                /// <summary>Verifies that the value is positive.</summary>
                /// <param name="actual">The actual value.</param>
                public static void ShouldBePositive(this int actual)
                {
                }

                /// <summary>Verifies that the value is even.</summary>
                /// <param name="actual">The actual value.</param>
                /// <returns>The actual value.</returns>
                public static int ShouldBeEven(this int actual) => actual;
            }

            internal static class UserMessageAssertions
            {
                public static void WithUserMessage(string userMessage, Action assertion) => assertion();

                public static T WithUserMessage<T>(string userMessage, Func<T> assertion) => assertion();

                public static Task WithUserMessage(string userMessage, Func<Task> assertion) => assertion();

                public static Task<T> WithUserMessage<T>(string userMessage, Func<Task<T>> assertion) => assertion();
            }
        }
        """;

    // The paramName overload already uses the (Action, string) signature that the userMessage overload needs.
    /// <summary>A <c>ShouldFail</c> assertion whose <c>(Action, string?)</c> overload already occupies the signature a <c>userMessage</c> overload would need.</summary>
    public const string BlockedUserMessageOverloadSourceFile = """
        namespace Sample.Assertions
        {
            public static class SampleAssertions
            {
                public static T ShouldFail<T>(this Action action)
                    where T : Exception => throw new InvalidOperationException();

                public static T ShouldFail<T>(this Action action, string? paramName)
                    where T : ArgumentException => throw new InvalidOperationException();

                public static T ShouldFail<T>(this Action action, string? paramName, string userMessage)
                    where T : ArgumentException => throw new InvalidOperationException();
            }
        }
        """;

    /// <summary>Creates an empty directory named <c>sk-codefix-runner-</c> plus a new GUID under the system temp path.</summary>
    /// <returns>The full path of the created directory.</returns>
    public static string CreateTemporaryProject()
    {
        var projectDirectory = Path.Combine(Path.GetTempPath(), "sk-codefix-runner-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(projectDirectory);
        return projectDirectory;
    }

    // The unrestored project gets framework references only from a targeting pack that the selected SDK ships. The
    // repository's pinned SDK ships one; a newer installed SDK can need a downloaded pack and then loads no references.
    /// <summary>Copies the repository <c>global.json</c> into the directory so the sample project loads with the pinned SDK.</summary>
    /// <param name="directory">The directory that receives the copy.</param>
    public static void PinRepositorySdk(string directory)
    {
        File.Copy(Path.Combine(RepositoryFiles.GetRootPath(), "global.json"), Path.Combine(directory, "global.json"));
    }

    /// <summary>Writes <see cref="ProjectFile"/> as <c>Sample.Tests.csproj</c> and the given source as <c>SampleTests.cs</c> into the directory.</summary>
    /// <param name="directory">The directory to write into.</param>
    /// <param name="source">The text of the source file.</param>
    /// <param name="ct">The token that cancels the writes.</param>
    /// <returns>The paths of the written project file and source file.</returns>
    public static async Task<(string ProjectPath, string SourcePath)> Write(string directory, string source, CancellationToken ct)
    {
        var projectPath = Path.Combine(directory, "Sample.Tests.csproj");
        var sourcePath = Path.Combine(directory, "SampleTests.cs");
        await File.WriteAllTextAsync(projectPath, ProjectFile, ct);
        await File.WriteAllTextAsync(sourcePath, source, ct);
        return (projectPath, sourcePath);
    }
}
