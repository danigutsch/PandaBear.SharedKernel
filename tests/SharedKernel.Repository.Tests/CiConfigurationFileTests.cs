using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests;

/// <summary>Guards the strict format of <c>.config/sharedkernel-ci.json</c>.</summary>
public sealed class CiConfigurationFileTests : IDisposable
{
    private readonly RepositoryTestScope scope = new();

    [Fact]
    public async Task The_configuration_is_read_with_its_optional_slice_settings()
    {
        // Arrange
        scope.WriteFile(".config/sharedkernel-ci.json", RepositoryTestScope.DefaultConfiguration.Replace(
            """{ "name": "library", "projects": [] }""", """{ "name": "library", "projects": [], "openApi": ["admin"], "playwright": true }""", StringComparison.Ordinal));

        // Act
        var configuration = await CiConfigurationFile.Read(scope.Root, TestContext.Current.CancellationToken);

        // Assert
        configuration.Solution.ShouldBe("SharedKernel.slnx");
        var slice = configuration.Slices.ShouldHaveSingleItem();
        slice.OpenApi.ShouldBe(["admin"]);
        slice.Playwright.ShouldBeTrue();
        configuration.DefaultSlice.ShouldBe("library");
    }

    [Theory]
    [InlineData("\"version\": 1", "\"version\": 2", ": $.version must be 1.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"extra\": true,", " is not valid at $.extra (line 2).")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"version\": 1,", " is not valid at $.version (line 2).")]
    [InlineData("\"requireCoveringTests\": false", "\"requireCoveringTests\": false,", " is not valid at")]
    [InlineData(",\n    \"requireCoveringTests\": false", "", " is not valid at $")]
    [InlineData("\"solution\": \"SharedKernel.slnx\"", "\"solution\": \"../SharedKernel.slnx\"", ": $.solution must be a repository-relative .slnx path")]
    [InlineData("\"documentation\": [\"docs/*\", \"README.md\"]", "\"documentation\": [\"docs\\\\*\"]", ": $.selection.documentation[0] must be a non-empty value with '/' separators.")]
    [InlineData("\"alwaysSelected\": []", "\"alwaysSelected\": [\"/tests/A.csproj\"]", ": $.selection.alwaysSelected[0] must be a repository-relative project path")]
    [InlineData("\"name\": \"library\"", "\"name\": \"Library\"", ": $.slices[0].name must be lowercase letters and digits")]
    [InlineData("\"name\": \"library\"", "\"name\": \"build\"", ": $.slices[0].name must be lowercase letters and digits")]
    [InlineData("\"defaultSlice\": \"library\"", "\"defaultSlice\": \"other\"", ": $.slices[0].projects must name at least one test project unless the slice is the default slice.")]
    [InlineData("\"slices\": [{ \"name\": \"library\", \"projects\": [] }]", "\"slices\": []", ": $.slices must name at least one slice.")]
    [InlineData("\"slices\": [{ \"name\": \"library\", \"projects\": [] }]", "\"slices\": [{ \"name\": \"library\", \"projects\": [] }, { \"name\": \"library\", \"projects\": [\"tests/A/A.csproj\"] }]", ": $.slices[1].name repeats the slice library.")]
    public async Task Invalid_configuration_names_the_file_and_the_json_path(string original, string replacement, string expected)
    {
        // Arrange
        var content = RepositoryTestScope.DefaultConfiguration.Replace(original, replacement, StringComparison.Ordinal);
        content.ShouldNotBe(RepositoryTestScope.DefaultConfiguration);
        scope.WriteFile(".config/sharedkernel-ci.json", content);

        // Act
        Func<Task> read = () => CiConfigurationFile.Read(scope.Root, TestContext.Current.CancellationToken);
        var exception = await read.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldStartWith(".config/sharedkernel-ci.json" + expected, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_configuration_names_the_file()
    {
        // Arrange
        File.Delete(Path.Combine(scope.Root, ".config", "sharedkernel-ci.json"));

        // Act
        Func<Task> read = () => CiConfigurationFile.Read(scope.Root, TestContext.Current.CancellationToken);
        var exception = await read.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe(".config/sharedkernel-ci.json does not exist.");
    }

    /// <inheritdoc />
    public void Dispose() => scope.Dispose();
}
