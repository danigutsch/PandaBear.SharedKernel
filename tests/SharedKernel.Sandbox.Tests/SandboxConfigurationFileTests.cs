namespace PandaBear.Sandbox.Tests;

/// <summary>Tests reading and validating <c>.config/sharedkernel-sandbox.json</c>.</summary>
[Trait("Capability", "Sandbox")]
public sealed class SandboxConfigurationFileTests
{
    [Fact]
    public async Task A_missing_file_is_no_configuration()
    {
        // Arrange
        using var root = new TemporaryDirectory();

        // Act
        var configuration = await SandboxConfigurationFile.Read(root.Path, TestContext.Current.CancellationToken);

        // Assert
        configuration.ShouldBeNull();
    }

    [Fact]
    public async Task A_valid_file_is_read_with_its_settings_section()
    {
        // Arrange
        using var root = ConfigurationRepository.Create("""{ "version": 1, "deniedReadPaths": ["~/.ssh"], "settings": { "profile": "dotnet", "allowWrite": ["./out"] } }""");

        // Act
        var configuration = await SandboxConfigurationFile.Read(root.Path, TestContext.Current.CancellationToken);

        // Assert
        configuration.ShouldNotBeNull();
        configuration.DeniedReadPaths.ShouldBe(["~/.ssh"]);
        configuration.Settings.ShouldNotBeNull();
        configuration.Settings.Profile.ShouldBe("dotnet");
        configuration.Settings.AllowWrite.ShouldBe(["./out"]);
        configuration.Settings.Environment.ShouldBeTrue();
    }

    [Theory]
    [InlineData("""{ "version": 1, "deniedReadPaths": [], "settings": { "profile": "node" } }""", "$.settings.profile must be \"dotnet\" when given.")]
    [InlineData("""{ "version": 1, "deniedReadPaths": [], "settings": { "writeDotnetHome": true } }""", "$.settings writeNuGetFolders, writeDotnetHome, and allowSdkDownload need \"profile\": \"dotnet\".")]
    [InlineData("""{ "version": 1, "deniedReadPaths": [], "settings": { "allowWrite": ["/"] } }""", "$.settings.allowWrite[0] would make the whole filesystem or the whole home folder writable; name the folder that needs it.")]
    [InlineData("""{ "version": 1, "deniedReadPaths": [], "settings": { "allowedDomains": ["*.co.uk"] } }""", "$.settings.allowedDomains[0] must be a host name")]
    [InlineData("""{ "version": 1, "deniedReadPaths": [], "settings": { "target": "../settings.json" } }""", "$.settings.target must be a repository-relative path")]
    public async Task An_invalid_settings_section_names_the_file_and_the_JSON_path(string json, string expected)
    {
        // Arrange
        using var root = ConfigurationRepository.Create(json);

        // Act
        var exception = await new Func<Task>(() => SandboxConfigurationFile.Read(root.Path, TestContext.Current.CancellationToken)).ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldStartWith(".config/sharedkernel-sandbox.json: " + expected, StringComparison.Ordinal);
    }
}
