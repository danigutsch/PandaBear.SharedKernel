using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests;

/// <summary>Guards the strict format of <c>.config/sharedkernel-ci.json</c>.</summary>
public sealed class CiConfigurationFileTests : IDisposable
{
    private const string AdminTarget =
        """{ "name": "admin", "project": "src/Admin/Admin.csproj", "generateProperty": "GenerateAdminOpenApi", "refreshProperty": "RefreshAdminOpenApi" }""";

    private const string Library = "\"name\": \"library\", \"projects\": []";

    private const string HostService =
        """{ "project": "tests/Host/Host.csproj", "readyFileVariable": "APP_READY_FILE", "valueVariable": "APP_VALUE" }""";

    private readonly RepositoryTestScope scope = new();

    [Fact]
    public async Task An_OpenApi_target_reads_its_optional_preserved_environment_variables()
    {
        // Arrange
        scope.WriteFile(".config/sharedkernel-ci.json", RepositoryTestScope.DefaultConfiguration.Replace(
            "\"version\": 1,",
            "\"version\": 1,\n  \"openApi\": [{ \"name\": \"admin\", \"project\": \"src/Admin/Admin.csproj\", \"generateProperty\": \"G\", \"refreshProperty\": \"R\", \"preserveEnvironment\": [\"REMOTE_CONTAINERS\"] }],",
            StringComparison.Ordinal));

        // Act
        var configuration = await CiConfigurationFile.Read(scope.Root, TestContext.Current.CancellationToken);

        // Assert
        var target = configuration.OpenApi.ShouldNotBeNull().ShouldHaveSingleItem();
        target.PreserveEnvironment.ShouldBe(["REMOTE_CONTAINERS"]);
    }

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

    [Fact]
    public async Task A_slice_service_defaults_its_timeouts_to_an_image_pull_and_a_container_stop()
    {
        // Arrange
        scope.WriteFile(".config/sharedkernel-ci.json", RepositoryTestScope.DefaultConfiguration.Replace(
            Library, Library + ", \"service\": " + HostService, StringComparison.Ordinal));

        // Act
        var configuration = await CiConfigurationFile.Read(scope.Root, TestContext.Current.CancellationToken);

        // Assert
        configuration.Slices.ShouldHaveSingleItem().Service.ShouldBe(new ServiceConfiguration("tests/Host/Host.csproj", "APP_READY_FILE", "APP_VALUE", 300, 60));
    }

    [Fact]
    public async Task A_slice_service_reads_its_timeouts()
    {
        // Arrange
        scope.WriteFile(".config/sharedkernel-ci.json", RepositoryTestScope.DefaultConfiguration.Replace(
            Library,
            Library + ", \"service\": { \"project\": \"tests/Host/Host.csproj\", \"readyFileVariable\": \"APP_READY_FILE\", \"valueVariable\": \"APP_VALUE\", \"readyTimeoutSeconds\": 3600, \"stopTimeoutSeconds\": 1 }",
            StringComparison.Ordinal));

        // Act
        var configuration = await CiConfigurationFile.Read(scope.Root, TestContext.Current.CancellationToken);

        // Assert
        configuration.Slices.ShouldHaveSingleItem().Service.ShouldBe(new ServiceConfiguration("tests/Host/Host.csproj", "APP_READY_FILE", "APP_VALUE", 3600, 1));
    }

    [Fact]
    public async Task A_slice_without_a_service_reads_none()
    {
        // Act
        var configuration = await CiConfigurationFile.Read(scope.Root, TestContext.Current.CancellationToken);

        // Assert
        configuration.Slices.ShouldHaveSingleItem().Service.ShouldBeNull();
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
    [InlineData("\"requireCoveringTests\": false", "\"requireCoveringTests\": false, \"testPaths\": [{ \"paths\": [], \"tests\": [\"tests/A/A.csproj\"] }]", ": $.selection.testPaths[0].paths must name at least one pattern.")]
    [InlineData("\"requireCoveringTests\": false", "\"requireCoveringTests\": false, \"testPaths\": [{ \"paths\": [\"a\\\\b\"], \"tests\": [\"tests/A/A.csproj\"] }]", ": $.selection.testPaths[0].paths[0] must be a non-empty value with '/' separators.")]
    [InlineData("\"requireCoveringTests\": false", "\"requireCoveringTests\": false, \"testPaths\": [{ \"paths\": [\"a/*\"], \"tests\": [] }]", ": $.selection.testPaths[0].tests must name at least one test project.")]
    [InlineData("\"requireCoveringTests\": false", "\"requireCoveringTests\": false, \"testPaths\": [{ \"paths\": [\"a/*\"], \"tests\": [\"/tests/A.csproj\"] }]", ": $.selection.testPaths[0].tests[0] must be a repository-relative project path")]
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
    [InlineData("\"version\": 1,", "\"version\": 1, \"openApi\": [{ \"name\": \"admin\", \"project\": \"src/A/A.csproj\", \"generateProperty\": \"G\", \"refreshProperty\": \"R\", \"preserveEnvironment\": [\"A=B\"] }],", ": $.openApi[0].preserveEnvironment[0] must be an environment variable name.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"openApi\": [{ \"name\": \"admin\", \"project\": \"src/A/A.csproj\", \"generateProperty\": \"G\", \"refreshProperty\": \"R\", \"preserveEnvironment\": [\"A\", \"A\"] }],", ": $.openApi[0].preserveEnvironment repeats A.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"openApi\": [{ \"name\": \"admin\", \"project\": \"src/A/A.csproj\", \"generateProperty\": \"G\", \"refreshProperty\": \"R\", \"preserveEnvironment\": [\"GITHUB_TOKEN\"] }],", ": $.openApi[0].preserveEnvironment[0] must not name a credential variable.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"openApi\": [{ \"name\": \"admin\", \"project\": \"src/A/A.csproj\", \"generateProperty\": \"G\", \"refreshProperty\": \"R\", \"preserveEnvironment\": [\"gh_token\"] }],", ": $.openApi[0].preserveEnvironment[0] must not name a credential variable.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"openApi\": [{ \"name\": \"admin\", \"project\": \"src/A/A.csproj\", \"generateProperty\": \"G\", \"refreshProperty\": \"R\", \"preserveEnvironment\": [\"Db_Password\"] }],", ": $.openApi[0].preserveEnvironment[0] must not name a credential variable.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"openApi\": [{ \"name\": \"admin\", \"project\": \"src/A/A.csproj\", \"generateProperty\": \"G\", \"refreshProperty\": \"R\", \"preserveEnvironment\": [\"API_KEY\"] }],", ": $.openApi[0].preserveEnvironment[0] must not name a credential variable.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"openApi\": [{ \"name\": \"admin\", \"project\": \"src/A/A.csproj\", \"generateProperty\": \"G\", \"refreshProperty\": \"R\", \"preserveEnvironment\": [\"CLIENT_SECRET\"] }],", ": $.openApi[0].preserveEnvironment[0] must not name a credential variable.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"openApi\": [{ \"name\": \"admin\", \"project\": \"src/A/A.csproj\", \"generateProperty\": \"G\", \"refreshProperty\": \"R\", \"preserveEnvironment\": [\"AZURE_CREDENTIALS\"] }],", ": $.openApi[0].preserveEnvironment[0] must not name a credential variable.")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"openApi\": [{ \"name\": \"admin\", \"project\": \"src/A/A.csproj\", \"generateProperty\": \"G\", \"refreshProperty\": \"R\", \"preserveEnvironment\": [\"ROOT_PASSWD\"] }],", ": $.openApi[0].preserveEnvironment[0] must not name a credential variable.")]
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
    [InlineData(Library, Library + ", \"service\": { \"project\": \"tests/Host/Host.csproj\", \"readyFileVariable\": \"APP_READY_FILE\" }", " is not valid at $.slices[0].service")]
    [InlineData(Library, Library + ", \"service\": { \"project\": \"tests/Host/Host.csproj\", \"readyFileVariable\": \"APP_READY_FILE\", \"valueVariable\": \"APP_VALUE\", \"extra\": true }", " is not valid at $.slices[0].service.extra")]
    [InlineData(Library, Library + ", \"service\": { \"project\": \"tests/Host/Host.csproj\", \"readyFileVariable\": \"APP_READY_FILE\", \"valueVariable\": null }", " is not valid at $.slices[0].service.valueVariable")]
    [InlineData(Library, Library + ", \"service\": { \"project\": \"tests/Host/Host.csproj\", \"readyFileVariable\": \"APP_READY_FILE\", \"valueVariable\": \"APP_VALUE\", \"readyTimeoutSeconds\": \"soon\" }", " is not valid at $.slices[0].service.readyTimeoutSeconds")]
    [InlineData(Library, Library + ", \"service\": { \"project\": \"../Host.csproj\", \"readyFileVariable\": \"APP_READY_FILE\", \"valueVariable\": \"APP_VALUE\" }", ": $.slices[0].service.project must be a repository-relative project path")]
    [InlineData(Library, Library + ", \"service\": { \"project\": \"tests\\\\Host.csproj\", \"readyFileVariable\": \"APP_READY_FILE\", \"valueVariable\": \"APP_VALUE\" }", ": $.slices[0].service.project must be a repository-relative project path")]
    [InlineData("\"name\": \"library\", \"projects\": []", "\"name\": \"library\", \"projects\": [\"tests/A/A.csproj\"], \"service\": { \"project\": \"tests/A/A.csproj\", \"readyFileVariable\": \"APP_READY_FILE\", \"valueVariable\": \"APP_VALUE\" }", ": $.slices[0].service.project names tests/A/A.csproj, which slice library lists as a test project; a service is not a test project.")]
    [InlineData(Library, Library + ", \"service\": { \"project\": \"tests/Host/Host.csproj\", \"readyFileVariable\": \"1_READY\", \"valueVariable\": \"APP_VALUE\" }", ": $.slices[0].service.readyFileVariable must be an environment variable name: letters, digits, and '_', not starting with a digit.")]
    [InlineData(Library, Library + ", \"service\": { \"project\": \"tests/Host/Host.csproj\", \"readyFileVariable\": \"APP_READY_FILE\", \"valueVariable\": \"APP-VALUE\" }", ": $.slices[0].service.valueVariable must be an environment variable name: letters, digits, and '_', not starting with a digit.")]
    [InlineData(Library, Library + ", \"service\": { \"project\": \"tests/Host/Host.csproj\", \"readyFileVariable\": \"APP_READY_FILE\", \"valueVariable\": \"APP=VALUE\" }", ": $.slices[0].service.valueVariable must be an environment variable name: letters, digits, and '_', not starting with a digit.")]
    [InlineData(Library, Library + ", \"service\": { \"project\": \"tests/Host/Host.csproj\", \"readyFileVariable\": \"GITHUB_TOKEN\", \"valueVariable\": \"APP_VALUE\" }", ": $.slices[0].service.readyFileVariable names GITHUB_TOKEN, a GitHub token variable that the tool removes from the service's environment; use another name.")]
    [InlineData(Library, Library + ", \"service\": { \"project\": \"tests/Host/Host.csproj\", \"readyFileVariable\": \"APP_READY_FILE\", \"valueVariable\": \"gh_token\" }", ": $.slices[0].service.valueVariable names gh_token, a GitHub token variable that the tool removes from the service's environment; use another name.")]
    [InlineData(Library, Library + ", \"service\": { \"project\": \"tests/Host/Host.csproj\", \"readyFileVariable\": \"APP_VALUE\", \"valueVariable\": \"APP_VALUE\" }", ": $.slices[0].service.valueVariable repeats readyFileVariable APP_VALUE.")]
    [InlineData(Library, Library + ", \"service\": { \"project\": \"tests/Host/Host.csproj\", \"readyFileVariable\": \"APP_READY_FILE\", \"valueVariable\": \"APP_VALUE\", \"readyTimeoutSeconds\": 0 }", ": $.slices[0].service.readyTimeoutSeconds must be from 1 to 3600.")]
    [InlineData(Library, Library + ", \"service\": { \"project\": \"tests/Host/Host.csproj\", \"readyFileVariable\": \"APP_READY_FILE\", \"valueVariable\": \"APP_VALUE\", \"readyTimeoutSeconds\": 3601 }", ": $.slices[0].service.readyTimeoutSeconds must be from 1 to 3600.")]
    [InlineData(Library, Library + ", \"service\": { \"project\": \"tests/Host/Host.csproj\", \"readyFileVariable\": \"APP_READY_FILE\", \"valueVariable\": \"APP_VALUE\", \"stopTimeoutSeconds\": 0 }", ": $.slices[0].service.stopTimeoutSeconds must be from 1 to 3600.")]
    [InlineData(Library, Library + ", \"service\": { \"project\": \"tests/Host/Host.csproj\", \"readyFileVariable\": \"APP_READY_FILE\", \"valueVariable\": \"APP_VALUE\", \"stopTimeoutSeconds\": -5 }", ": $.slices[0].service.stopTimeoutSeconds must be from 1 to 3600.")]
    [InlineData("\"slices\": [{ \"name\": \"library\", \"projects\": [] }]", "\"slices\": [{ \"name\": \"library\", \"projects\": [], \"service\": " + HostService + " }, { \"name\": \"other\", \"projects\": [\"tests/A/A.csproj\"], \"service\": { \"project\": \"tests/Other/Other.csproj\", \"readyFileVariable\": \"OTHER_READY_FILE\", \"valueVariable\": \"APP_VALUE\" } }]", ": $.slices[1].service.valueVariable repeats APP_VALUE, which the service of slice library already hands to its tests.")]
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
