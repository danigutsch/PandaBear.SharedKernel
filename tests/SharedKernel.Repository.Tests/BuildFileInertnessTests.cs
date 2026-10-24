using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests;

/// <summary>Guards which tracked build files let documentation changes skip validation.</summary>
public sealed class BuildFileInertnessTests : IDisposable
{
    private const string ParentProps = "$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)..'))";

    private readonly RepositoryTestScope scope = new();

    [Theory]
    [InlineData("src/Core/Core.csproj", """<Project><Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" /><Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" /></Project>""", null)]
    [InlineData("src/Directory.Build.props", $"""<Project><Import Project="{ParentProps}" /></Project>""", null)]
    [InlineData("src/Directory.Build.props", """<Project><Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" Condition="true" /></Project>""", null)]
    [InlineData("src/Directory.Build.targets", """<Project><Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.targets', '$(MSBuildThisFileDirectory)..'))" /></Project>""", null)]
    [InlineData("src/Custom.props", $"""<Project><Import Project="{ParentProps}" /></Project>""", "src/Custom.props imports a file other than the SDK or the Custom.props above it.")]
    [InlineData("src/Core/Core.csproj", """<Project><Import Project="custom.targets" Sdk="Microsoft.NET.Sdk" /></Project>""", "src/Core/Core.csproj imports a file other than the SDK or the Core.csproj above it.")]
    [InlineData("src/Core/Core.csproj", """<Project><Import Project="Sdk.targets" Sdk="Custom.Sdk" /></Project>""", "src/Core/Core.csproj imports a file other than the SDK or the Core.csproj above it.")]
    [InlineData("src/Core/Core.csproj", """<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><Import Project="$(External)/Sdk.targets" /></Project>""", "src/Core/Core.csproj imports a file other than the SDK or the Core.csproj above it.")]
    [InlineData("src/Core/Core.csproj", """<Project><Target Name="T"><Exec Command="cat docs/x.md" /></Target></Project>""", "src/Core/Core.csproj uses Exec.")]
    [InlineData("src/Core/Core.csproj", """<Project><UsingTask TaskName="T" AssemblyFile="t.dll" /></Project>""", "src/Core/Core.csproj uses UsingTask.")]
    [InlineData("src/Core/Core.csproj", """<Project><Target Name="T"><ReadLinesFromFile File="docs/x.md" /></Target></Project>""", "src/Core/Core.csproj uses ReadLinesFromFile.")]
    [InlineData("src/Core/Core.csproj", """<Project><PropertyGroup><Text>$([System.IO.File]::ReadAllText('docs/x.md'))</Text></PropertyGroup></Project>""", "src/Core/Core.csproj calls System.IO.File.")]
    [InlineData("src/Core/Core.csproj", "<Project>", "src/Core/Core.csproj cannot be read as MSBuild XML.")]
    public async Task Only_sdk_and_parent_directory_imports_without_file_reads_are_inert(string path, string content, string? expected)
    {
        // Arrange
        scope.WriteFile(path, content);

        // Act
        var reason = await BuildFileInertness.FindNonInert(scope.Root, ["src/Dependent/Dependent.csproj", path], TestContext.Current.CancellationToken);

        // Assert
        reason.ShouldBe(expected);
    }

    [Fact]
    public async Task A_listed_build_file_that_is_missing_is_not_inert()
    {
        // Act
        var reason = await BuildFileInertness.FindNonInert(scope.Root, ["src/Missing/Missing.csproj"], TestContext.Current.CancellationToken);

        // Assert
        reason.ShouldBe("src/Missing/Missing.csproj cannot be read as MSBuild XML.");
    }

    /// <inheritdoc />
    public void Dispose() => scope.Dispose();
}
