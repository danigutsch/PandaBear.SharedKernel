using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests;

/// <summary>Guards the strict format of <c>.config/sharedkernel-ci.json</c>.</summary>
public sealed class CiConfigurationFileTests : IDisposable
{
    private const string AdminTarget =
        """{ "name": "admin", "project": "src/Admin/Admin.csproj", "generateProperty": "GenerateAdminOpenApi", "refreshProperty": "RefreshAdminOpenApi" }""";

    private readonly RepositoryTestScope scope = new();

    [Fact]
    public async Task The_configuration_is_read_with_its_optional_OpenApi_and_slice_settings()
    {
        // Arrange
        scope.WriteFile(".config/sharedkernel-ci.json", RepositoryTestScope.DefaultConfiguration
            .Replace("\"version\": 1,", "\"version\": 1,\n  \"openApi\": [" + AdminTarget + "],", StringComparison.Ordinal)
            .Replace(
                """{ "name": "library", "projects": [] }""",
                """{ "name": "library", "projects": ["tests/Core.Tests/Core.Tests.csproj"], "openApi": ["admin"], "playwright": { "project": "tests/Core.Tests/Core.Tests.csproj", "browsers": ["chromium"] } }""",
                StringComparison.Ordinal));

        // Act
        var configuration = await CiConfigurationFile.Read(scope.Root, TestContext.Current.CancellationToken);

        // Assert
        configuration.Solution.ShouldBe("SharedKernel.slnx");
        configuration.OpenApi.ShouldBe([new OpenApiTargetConfiguration("admin", "src/Admin/Admin.csproj", "GenerateAdminOpenApi", "RefreshAdminOpenApi")]);
        var slice = configuration.Slices.ShouldHaveSingleItem();
        slice.OpenApi.ShouldBe(["admin"]);
        slice.Playwright.ShouldNotBeNull().Project.ShouldBe("tests/Core.Tests/Core.Tests.csproj");
        slice.Playwright.Browsers.ShouldBe(["chromium"]);
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
    [InlineData("\"name\": \"library\", \"projects\": []", "\"name\": \"library\", \"projects\": [], \"openApi\": [\"admin\"]", ": $.slices[0].openApi[0] names admin, which $.openApi does not define.")]
    [InlineData("\"name\": \"library\", \"projects\": []", "\"name\": \"library\", \"projects\": [], \"playwright\": { \"project\": \"tests/A/A.csproj\", \"browsers\": [\"chromium\"] }", ": $.slices[0].playwright.project must name a test project that the slice lists.")]
    [InlineData("\"name\": \"library\", \"projects\": []", "\"name\": \"library\", \"projects\": [\"tests/A/A.csproj\"], \"playwright\": { \"project\": \"tests/A/A.csproj\", \"browsers\": [\"--with-deps\"] }", ": $.slices[0].playwright.browsers[0] must be lowercase letters, digits, and '-', starting with a letter.")]
    [InlineData("\"name\": \"library\", \"projects\": []", "\"name\": \"library\", \"projects\": [\"tests/A/A.csproj\"], \"playwright\": { \"project\": \"tests/A/A.csproj\", \"browsers\": [] }", ": $.slices[0].playwright.browsers must name at least one browser.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"openApi\": [{ \"name\": \"Admin\", \"project\": \"src/A/A.csproj\", \"generateProperty\": \"G\", \"refreshProperty\": \"R\" }],", ": $.openApi[0].name must be lowercase letters and digits in words separated by '-'.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"openApi\": [{ \"name\": \"admin\", \"project\": \"../A.csproj\", \"generateProperty\": \"G\", \"refreshProperty\": \"R\" }],", ": $.openApi[0].project must be a repository-relative project path")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"openApi\": [{ \"name\": \"admin\", \"project\": \"src/A/A.csproj\", \"generateProperty\": \"G;X=1\", \"refreshProperty\": \"R\" }],", ": $.openApi[0].generateProperty must be an MSBuild property name.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"openApi\": [{ \"name\": \"admin\", \"project\": \"src/A/A.csproj\", \"generateProperty\": \"G\", \"refreshProperty\": \"-p:R\" }],", ": $.openApi[0].refreshProperty must be an MSBuild property name.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"openApi\": [" + AdminTarget + ", " + AdminTarget + "],", ": $.openApi[1].name repeats the OpenAPI target admin.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"coverage\": { \"thresholdPercent\": 101, \"include\": [\"src/*\"], \"exclude\": [], \"symbolDirectories\": [\"src\"] },", ": $.coverage.thresholdPercent must be from 0 to 100.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"coverage\": { \"thresholdPercent\": 80, \"include\": [], \"exclude\": [], \"symbolDirectories\": [\"src\"] },", ": $.coverage.include must name at least one pattern.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"coverage\": { \"thresholdPercent\": 80, \"include\": [\"src/*\"], \"exclude\": [\"src\\\\*\"], \"symbolDirectories\": [\"src\"] },", ": $.coverage.exclude[0] must be a non-empty value with '/' separators.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"coverage\": { \"thresholdPercent\": 80, \"include\": [\"src/*\"], \"exclude\": [], \"symbolDirectories\": [] },", ": $.coverage.symbolDirectories must name at least one directory.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"coverage\": { \"thresholdPercent\": 80, \"include\": [\"src/*\"], \"exclude\": [], \"symbolDirectories\": [\"../src\"] },", ": $.coverage.symbolDirectories[0] must be a repository-relative directory")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"coverage\": { \"thresholdPercent\": 80, \"include\": [\"src/*\"], \"symbolDirectories\": [\"src\"] },", " is not valid at $.coverage")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"mutation\": { \"strykerVersion\": \"4.15\", \"configFiles\": [\"tests/A/stryker.json\"] },", ": $.mutation.strykerVersion must be an exact NuGet version, such as 4.15.0.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"mutation\": { \"strykerVersion\": \"[4.15.0,)\", \"configFiles\": [\"tests/A/stryker.json\"] },", ": $.mutation.strykerVersion must be an exact NuGet version, such as 4.15.0.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"mutation\": { \"strykerVersion\": \"4.15.0\", \"configFiles\": [] },", ": $.mutation.configFiles must name at least one Stryker config file.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"mutation\": { \"strykerVersion\": \"4.15.0\", \"configFiles\": [\"../stryker.json\"] },", ": $.mutation.configFiles[0] must be a repository-relative file path")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"mutation\": { \"strykerVersion\": \"4.15.0\", \"configFiles\": [\"tests/A/stryker.json\", \"tests/A/stryker.json\"] },", ": $.mutation.configFiles[1] repeats tests/A/stryker.json.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"mutation\": { \"strykerVersion\": \"4.15.0\" },", " is not valid at $.mutation")]
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
