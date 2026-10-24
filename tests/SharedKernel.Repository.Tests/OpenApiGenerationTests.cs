using System.Diagnostics;
using PandaBear.CI.Tool;
using PandaBear.Repository.Tests.Conventions;

namespace PandaBear.Repository.Tests;

public sealed class OpenApiGenerationTests : IDisposable
{
    private readonly RepositoryTestScope scope = new();

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
            call.FileName.ShouldBe(OpenApiGeneration.DotnetHostPath());
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

    [Fact]
    public void The_generation_environment_names_the_host_environment_that_the_OpenApi_library_recognizes()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var publicApi = Directory.EnumerateFiles(Path.Combine(root, "src", "SharedKernel", "SharedKernel.OpenApi"), "PublicAPI.*.txt")
            .SelectMany(File.ReadLines)
            .ShouldHaveSingleItem(static line => line.StartsWith("const PandaBear.OpenApi.OpenApiGenerationMode.HostEnvironmentName = ", StringComparison.Ordinal));
        var startInfo = new ProcessStartInfo(Path.Combine(Path.GetTempPath(), "dotnet")) { WorkingDirectory = scope.Root };

        // Act
        OpenApiGeneration.ApplyEnvironment(startInfo);

        // Assert
        publicApi.ShouldBe("const PandaBear.OpenApi.OpenApiGenerationMode.HostEnvironmentName = \"" + startInfo.Environment["ASPNETCORE_ENVIRONMENT"] + "\" -> string!");
        startInfo.Environment["DOTNET_ENVIRONMENT"].ShouldBe(startInfo.Environment["ASPNETCORE_ENVIRONMENT"]);
    }

    [Fact]
    public void The_tool_host_is_an_absolute_dotnet_executable()
    {
        // Act
        var host = OpenApiGeneration.DotnetHostPath();

        // Assert
        Path.IsPathFullyQualified(host).ShouldBeTrue();
        Path.GetFileNameWithoutExtension(host).ShouldBe("dotnet");
        File.Exists(host).ShouldBeTrue();
    }

    [Fact]
    public void Generation_environment_rejects_a_relative_dotnet_host()
    {
        // Arrange
        var startInfo = new ProcessStartInfo("dotnet") { WorkingDirectory = scope.Root };

        // Act
        Action apply = () => OpenApiGeneration.ApplyEnvironment(startInfo);

        // Assert
        apply.ShouldThrow<InvalidOperationException>().Message.ShouldBe("The OpenAPI generation host must be an absolute dotnet path.");
    }

    [Fact]
    public void Generation_environment_removes_hostile_parent_settings()
    {
        // Arrange
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "sharedkernel-openapi-repository");
        var startInfo = new ProcessStartInfo(Path.Combine(Path.GetTempPath(), "dotnet"))
        {
            WorkingDirectory = repositoryRoot
        };
        startInfo.Environment["Authentication__Authority"] = "https://authority.example";
        startInfo.Environment["Authentication__Issuer"] = "https://issuer.example";
        startInfo.Environment["Authentication__AllowHttpDevelopmentAuthority"] = "true";
        startInfo.Environment["Authentication__ClientId"] = "client-id";
        startInfo.Environment["Authentication__ClientSecret"] = "client-secret";
        startInfo.Environment["Authentication__DataProtection__CertificatePath"] = "/certificate.pfx";
        startInfo.Environment["Authentication__DataProtection__CertificatePassword"] = "certificate-password";
        startInfo.Environment["ConnectionStrings__CatalogDatabase"] = "Host=database.example;Password=secret";
        startInfo.Environment["OTEL_EXPORTER_OTLP_ENDPOINT"] = "https://otel.example";
        startInfo.Environment["OTEL_EXPORTER_OTLP_HEADERS"] = "Authorization=secret";
        startInfo.Environment["OTEL_SDK_DISABLED"] = "false";
        startInfo.Environment["HTTP_PROXY"] = "https://proxy.example";
        startInfo.Environment["HTTPS_PROXY"] = "https://proxy.example";
        startInfo.Environment["ALL_PROXY"] = "https://proxy.example";
        startInfo.Environment["http_proxy"] = "https://proxy.example";
        startInfo.Environment["https_proxy"] = "https://proxy.example";
        startInfo.Environment["all_proxy"] = "https://proxy.example";
        startInfo.Environment["GITHUB_TOKEN"] = "token";
        startInfo.Environment["AZURE_CLIENT_SECRET"] = "secret";
        startInfo.Environment["DOTNET_CLI_HOME"] = "/dotnet-home";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "0";
        startInfo.Environment["DOTNET_STARTUP_HOOKS"] = "/startup-hook.dll";
        startInfo.Environment["MSBuildSDKsPath"] = "/msbuild-sdks";
        startInfo.Environment["NUGET_PACKAGES"] = Path.Combine(Path.GetTempPath(), "parent-nuget-packages");
        startInfo.Environment["NUGET_CREDENTIALPROVIDERS_PATH"] = "/nuget-credentials";
        startInfo.Environment["NUGET_UNTRUSTED_PARENT_VALUE"] = "sentinel";
        startInfo.Environment["OpenApiToolTests__Unrelated"] = "sentinel";

        // Act
        OpenApiGeneration.ApplyEnvironment(startInfo);
        var dotnetDirectory = Path.GetDirectoryName(startInfo.FileName).ShouldNotBeNull();

        // Assert
        startInfo.Environment.ContainsKey("Authentication__Authority").ShouldBeFalse();
        startInfo.Environment.ContainsKey("Authentication__Issuer").ShouldBeFalse();
        startInfo.Environment.ContainsKey("Authentication__AllowHttpDevelopmentAuthority").ShouldBeFalse();
        startInfo.Environment.ContainsKey("Authentication__ClientId").ShouldBeFalse();
        startInfo.Environment.ContainsKey("Authentication__ClientSecret").ShouldBeFalse();
        startInfo.Environment.ContainsKey("Authentication__DataProtection__CertificatePath").ShouldBeFalse();
        startInfo.Environment.ContainsKey("Authentication__DataProtection__CertificatePassword").ShouldBeFalse();
        startInfo.Environment.ContainsKey("ConnectionStrings__CatalogDatabase").ShouldBeFalse();
        startInfo.Environment.ContainsKey("OTEL_EXPORTER_OTLP_ENDPOINT").ShouldBeFalse();
        startInfo.Environment.ContainsKey("OTEL_EXPORTER_OTLP_HEADERS").ShouldBeFalse();
        startInfo.Environment.ContainsKey("HTTP_PROXY").ShouldBeFalse();
        startInfo.Environment.ContainsKey("HTTPS_PROXY").ShouldBeFalse();
        startInfo.Environment.ContainsKey("ALL_PROXY").ShouldBeFalse();
        startInfo.Environment.ContainsKey("http_proxy").ShouldBeFalse();
        startInfo.Environment.ContainsKey("https_proxy").ShouldBeFalse();
        startInfo.Environment.ContainsKey("all_proxy").ShouldBeFalse();
        startInfo.Environment.ContainsKey("GITHUB_TOKEN").ShouldBeFalse();
        startInfo.Environment.ContainsKey("AZURE_CLIENT_SECRET").ShouldBeFalse();
        startInfo.Environment.ContainsKey("DOTNET_CLI_HOME").ShouldBeFalse();
        startInfo.Environment.ContainsKey("DOTNET_STARTUP_HOOKS").ShouldBeFalse();
        startInfo.Environment.ContainsKey("MSBuildSDKsPath").ShouldBeFalse();
        startInfo.Environment.ContainsKey("NUGET_PACKAGES").ShouldBeFalse();
        startInfo.Environment.ContainsKey("NUGET_CREDENTIALPROVIDERS_PATH").ShouldBeFalse();
        startInfo.Environment.ContainsKey("NUGET_UNTRUSTED_PARENT_VALUE").ShouldBeFalse();
        startInfo.Environment.ContainsKey("OpenApiToolTests__Unrelated").ShouldBeFalse();
        startInfo.Environment.ContainsKey("OpenApi__BuildGeneration").ShouldBeFalse();
        startInfo.Environment["PATH"].ShouldContain(dotnetDirectory, StringComparison.Ordinal);
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"].ShouldBe("1");
        startInfo.Environment["OTEL_SDK_DISABLED"].ShouldBe("true");
    }

    [Fact]
    public void Generation_environment_preserves_the_repository_nuget_cache()
    {
        // Arrange
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "sharedkernel-openapi-repository");
        var expectedPackagesPath = Path.Combine(repositoryRoot, ".nuget", "packages");
        var startInfo = new ProcessStartInfo(Path.Combine(Path.GetTempPath(), "dotnet"))
        {
            WorkingDirectory = repositoryRoot
        };
        startInfo.Environment["NUGET_PACKAGES"] = expectedPackagesPath;
        startInfo.Environment["NUGET_CREDENTIALPROVIDERS_PATH"] = "/nuget-credentials";

        // Act
        OpenApiGeneration.ApplyEnvironment(startInfo);

        // Assert
        startInfo.Environment["NUGET_PACKAGES"].ShouldBe(expectedPackagesPath);
        startInfo.Environment.ContainsKey("NUGET_CREDENTIALPROVIDERS_PATH").ShouldBeFalse();
    }

    [Fact]
    public void Generation_environment_preserves_required_windows_profile_and_temp_paths()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "sharedkernel-openapi-repository");
        var expectedPackagesPath = Path.Combine(repositoryRoot, ".nuget", "packages");
        var startInfo = new ProcessStartInfo(Path.Combine(Path.GetTempPath(), "dotnet"))
        {
            WorkingDirectory = repositoryRoot
        };
        startInfo.Environment["TEMP"] = "C:\\temp";
        startInfo.Environment["TMP"] = "C:\\tmp";
        startInfo.Environment["USERPROFILE"] = "C:\\Users\\SharedKernel";
        startInfo.Environment["APPDATA"] = "C:\\Users\\SharedKernel\\AppData\\Roaming";
        startInfo.Environment["LOCALAPPDATA"] = "C:\\Users\\SharedKernel\\AppData\\Local";
        startInfo.Environment["NUGET_PACKAGES"] = expectedPackagesPath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        startInfo.Environment["HOME"] = "/home/sharedkernel";
        startInfo.Environment["TMPDIR"] = "/tmp/sharedkernel";

        // Act
        OpenApiGeneration.ApplyEnvironment(startInfo);

        // Assert
        startInfo.Environment["TEMP"].ShouldBe("C:\\temp");
        startInfo.Environment["TMP"].ShouldBe("C:\\tmp");
        startInfo.Environment["USERPROFILE"].ShouldBe("C:\\Users\\SharedKernel");
        startInfo.Environment["APPDATA"].ShouldBe("C:\\Users\\SharedKernel\\AppData\\Roaming");
        startInfo.Environment["LOCALAPPDATA"].ShouldBe("C:\\Users\\SharedKernel\\AppData\\Local");
        startInfo.Environment["NUGET_PACKAGES"].ShouldBe(expectedPackagesPath);
        startInfo.Environment.ContainsKey("HOME").ShouldBeFalse();
        startInfo.Environment.ContainsKey("TMPDIR").ShouldBeFalse();
    }

    [Fact]
    public void Generation_environment_preserves_required_unix_profile_and_temp_paths()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var startInfo = new ProcessStartInfo(Path.Combine(Path.GetTempPath(), "dotnet"));
        startInfo.Environment["HOME"] = "/home/sharedkernel";
        startInfo.Environment["TMPDIR"] = "/tmp/sharedkernel";
        startInfo.Environment["TEMP"] = "C:\\temp";
        startInfo.Environment["TMP"] = "C:\\tmp";
        startInfo.Environment["USERPROFILE"] = "C:\\Users\\SharedKernel";
        startInfo.Environment["APPDATA"] = "C:\\Users\\SharedKernel\\AppData\\Roaming";
        startInfo.Environment["LOCALAPPDATA"] = "C:\\Users\\SharedKernel\\AppData\\Local";

        // Act
        OpenApiGeneration.ApplyEnvironment(startInfo);

        // Assert
        startInfo.Environment["HOME"].ShouldBe("/home/sharedkernel");
        startInfo.Environment["TMPDIR"].ShouldBe("/tmp/sharedkernel");
        startInfo.Environment.ContainsKey("TEMP").ShouldBeFalse();
        startInfo.Environment.ContainsKey("TMP").ShouldBeFalse();
        startInfo.Environment.ContainsKey("USERPROFILE").ShouldBeFalse();
        startInfo.Environment.ContainsKey("APPDATA").ShouldBeFalse();
        startInfo.Environment.ContainsKey("LOCALAPPDATA").ShouldBeFalse();
    }

    /// <inheritdoc />
    public void Dispose() => scope.Dispose();
}
