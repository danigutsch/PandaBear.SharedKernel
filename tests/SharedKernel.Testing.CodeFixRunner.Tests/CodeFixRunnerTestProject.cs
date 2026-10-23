using System.Globalization;

namespace PandaBear.Testing.CodeFixRunner.Tests;

internal static class CodeFixRunnerTestProject
{
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

    public const string MalformedProjectFile = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
        """;

    public const string MissingSdkGlobalJsonFile = """
        {
          "sdk": {
            "version": "10.0.999",
            "rollForward": "disable"
          }
        }
        """;

    public const string SolutionWithMissingProjectFile = """
        <Solution>
          <Project Path="Sample.Tests.csproj" />
          <Project Path="Missing/Missing.Tests.csproj" />
        </Solution>
        """;

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

    public const string UserMessageOverloadEditorConfigFile = """
        root = true

        [*.cs]
        dotnet_diagnostic.SKTEST017.severity = warning
        """;

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

    public static string CreateTemporaryProject()
    {
        var projectDirectory = Path.Combine(Path.GetTempPath(), "sk-codefix-runner-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(projectDirectory);
        return projectDirectory;
    }

    // The unrestored project gets framework references only from a targeting pack that the selected SDK ships. The
    // repository's pinned SDK ships one; a newer installed SDK can need a downloaded pack and then loads no references.
    public static void PinRepositorySdk(string directory)
    {
        File.Copy(Path.Combine(RepositoryFiles.GetRootPath(), "global.json"), Path.Combine(directory, "global.json"));
    }

    public static async Task<(string ProjectPath, string SourcePath)> Write(string directory, string source, CancellationToken ct)
    {
        var projectPath = Path.Combine(directory, "Sample.Tests.csproj");
        var sourcePath = Path.Combine(directory, "SampleTests.cs");
        await File.WriteAllTextAsync(projectPath, ProjectFile, ct);
        await File.WriteAllTextAsync(sourcePath, source, ct);
        return (projectPath, sourcePath);
    }
}
