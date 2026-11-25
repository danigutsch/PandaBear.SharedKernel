namespace SharedKernel.Sandbox.Tests;

/// <summary>Tests how the configuration, the dotnet profile, and the risk flags become the desired entries.</summary>
[Trait(TestTraitNames.CapabilityName, "Sandbox")]
public sealed class DesiredSettingsTests
{
    private static readonly SettingsRiskFlags NoFlags = new(false, false, false);
    private static readonly string[] Homes = ["/home/user"];

    [Fact]
    public void The_dotnet_profile_adds_the_feed_domain_and_environment_and_the_opt_ins_only_when_asked()
    {
        // Arrange
        var plain = new SandboxConfiguration(1, [], new SettingsConfiguration(Profile: "dotnet"));
        var opted = new SandboxConfiguration(1, [], new SettingsConfiguration(Profile: "dotnet", WriteNuGetFolders: true, WriteDotnetHome: true, AllowSdkDownload: true, Environment: false));

        // Act
        var plainSettings = DesiredSettings.Build(plain, NoFlags, Homes, enforce: true);
        var optedSettings = DesiredSettings.Build(opted, NoFlags, Homes, enforce: true);

        // Assert
        plainSettings.AllowedDomains.ShouldBe(["api.nuget.org"]);
        plainSettings.AllowWrite.ShouldBeEmpty();
        plainSettings.Environment.Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe(["DOTNET_CLI_TELEMETRY_OPTOUT", "DOTNET_CLI_USE_MSBUILD_SERVER", "MSBUILDDISABLENODEREUSE"]);
        optedSettings.AllowedDomains.ShouldBe(["api.nuget.org", "builds.dotnet.microsoft.com"]);
        optedSettings.AllowWrite.ShouldBe(["~/.nuget/packages", "~/.local/share/NuGet", "~/.dotnet"]);
        optedSettings.Environment.ShouldBeEmpty();
    }

    [Fact]
    public void Denied_read_paths_join_deny_read_without_duplicates_and_the_target_stays_unset()
    {
        // Arrange
        var configuration = new SandboxConfiguration(1, ["~/.ssh"], new SettingsConfiguration(DenyRead: ["~/.ssh", "~/.aws"]));

        // Act
        var desired = DesiredSettings.Build(configuration, NoFlags, Homes, enforce: true);

        // Assert
        desired.DenyRead.ShouldBe(["~/.ssh", "~/.aws"]);
        desired.Target.ShouldBeNull();
    }

    [Fact]
    public void A_risky_request_is_refused_when_enforced_and_only_warned_about_otherwise()
    {
        // Arrange
        var configuration = new SandboxConfiguration(1, [], new SettingsConfiguration(AllowWrite: ["~/.ssh"], AllowAllUnixSockets: true));

        // Act
        var refusal = new Func<object?>(() => DesiredSettings.Build(configuration, NoFlags, Homes, enforce: true)).ShouldThrow<InvalidOperationException>();
        var preview = DesiredSettings.Build(configuration, NoFlags, Homes, enforce: false);
        var accepted = DesiredSettings.Build(configuration, new SettingsRiskFlags(AllowAllUnixSockets: true, ExcludedCommands: false, RemoveDenyRead: false, SensitiveWrite: true), Homes, enforce: true);

        // Assert
        refusal.Message.ShouldStartWith("Refused: $.settings.allowAllUnixSockets", StringComparison.Ordinal);
        preview.RiskWarnings.Count.ShouldBe(2);
        preview.RiskWarnings.ShouldContain(static warning => warning.StartsWith("would require --accept-risk-sensitive-write", StringComparison.Ordinal));
        accepted.RiskWarnings.ShouldBeEmpty();
        accepted.AllowAllUnixSockets.ShouldBeTrue();
    }

    [Fact]
    public void Removing_a_required_denied_read_path_is_an_error_even_with_the_flag()
    {
        // Arrange
        var configuration = new SandboxConfiguration(1, ["~/.ssh"], new SettingsConfiguration(RemoveDenyRead: ["~/.ssh/"]));

        // Act
        var exception = new Func<object?>(() => DesiredSettings.Build(configuration, new SettingsRiskFlags(false, false, RemoveDenyRead: true), Homes, enforce: true)).ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe(".config/sharedkernel-sandbox.json: $.settings.removeDenyRead names a path that $.deniedReadPaths or $.settings.denyRead requires.");
    }

    [Fact]
    public void A_configuration_without_a_settings_section_is_refused()
    {
        // Act
        var exception = new Func<object?>(() => DesiredSettings.Build(new SandboxConfiguration(1, []), NoFlags, Homes, enforce: false)).ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe(".config/sharedkernel-sandbox.json: $.settings is required for the settings command.");
    }
}
