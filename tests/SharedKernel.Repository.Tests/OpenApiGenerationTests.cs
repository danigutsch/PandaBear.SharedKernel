namespace SharedKernel.Repository.Tests;

/// <summary>Guards the <c>openapi generate</c> and <c>openapi refresh</c> commands and the sanitized environment their child builds run in.</summary>
public sealed class OpenApiGenerationTests : IDisposable
{
    private readonly RepositoryTestScope scope = new();

    /// <summary>Writes a CI configuration that defines the admin and catalog OpenAPI targets, each with its own generate and refresh property.</summary>
    public OpenApiGenerationTests() =>
        scope.WriteFile(".config/sharedkernel-ci.json", RepositoryTestScope.DefaultConfiguration.Replace(
            "\"version\": 1,",
            """
            "version": 1,
              "openApi": [
                { "name": "admin", "project": "src/Admin/Admin.csproj", "generateProperty": "GenerateAdmin", "refreshProperty": "RefreshAdmin" },
                { "name": "catalog", "project": "src/Catalog/Catalog.csproj", "generateProperty": "GenerateCatalog", "refreshProperty": "RefreshCatalog" }
              ],
            """,
            StringComparison.Ordinal));

    [Fact]
    public async Task Generate_builds_every_configured_target_with_its_generate_property_through_the_tool_host()
    {
        // Arrange
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["openapi", "generate"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        processes.Calls.Select(static call => string.Join(' ', call.Arguments)).ShouldBe(
        [
            "build --no-restore src/Admin/Admin.csproj -p:GenerateAdmin=true",
            "build --no-restore src/Catalog/Catalog.csproj -p:GenerateCatalog=true",
        ]);
        processes.Calls.ShouldAllSatisfy(call =>
        {
            call.FileName.ShouldBe(DotnetHost.Path);
            call.WorkingDirectory.ShouldBe(scope.Root);
        });
    }

    [Fact]
    public async Task Refresh_builds_only_the_named_targets_with_their_refresh_property()
    {
        // Arrange
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["openapi", "refresh", "--target", "catalog"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        processes.Calls.Select(static call => string.Join(' ', call.Arguments)).ShouldBe(["build --no-restore src/Catalog/Catalog.csproj -p:RefreshCatalog=true"]);
    }

    [Fact]
    public async Task An_unknown_target_fails_before_building()
    {
        // Arrange
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["openapi", "generate", "--target", "branding"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Unknown OpenAPI target: branding.", StringComparison.Ordinal);
        processes.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_configuration_without_OpenApi_targets_fails()
    {
        // Arrange
        scope.WriteFile(".config/sharedkernel-ci.json", RepositoryTestScope.DefaultConfiguration);
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["openapi", "generate"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(".config/sharedkernel-ci.json: $.openApi must define the targets that openapi builds.", StringComparison.Ordinal);
        processes.Calls.ShouldBeEmpty();
    }

    /// <inheritdoc />
    public void Dispose() => scope.Dispose();
}
