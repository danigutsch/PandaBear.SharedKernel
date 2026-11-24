namespace PandaBear.Sandbox.ClaudeCode.Tests;

/// <summary>Tests the mapping of the managed settings to Claude Code's JSON keys and the layout of the rewritten text.</summary>
[Trait(TestTraitNames.CapabilityName, "Sandbox")]
public sealed class ClaudeCodeSettingsDocumentTests
{
    // The keys of the Claude Code settings reference: https://code.claude.com/docs/en/settings-reference
    [Theory]
    [InlineData(SandboxSetting.AllowWrite, "$.sandbox.filesystem.allowWrite")]
    [InlineData(SandboxSetting.DenyWrite, "$.sandbox.filesystem.denyWrite")]
    [InlineData(SandboxSetting.DenyRead, "$.sandbox.filesystem.denyRead")]
    [InlineData(SandboxSetting.AllowedDomains, "$.sandbox.network.allowedDomains")]
    [InlineData(SandboxSetting.ExcludedCommands, "$.sandbox.excludedCommands")]
    [InlineData(SandboxSetting.AllowAllUnixSockets, "$.sandbox.network.allowAllUnixSockets")]
    [InlineData(SandboxSetting.Environment, "$.env")]
    public void Each_setting_maps_to_its_documented_key(SandboxSetting setting, string location) =>
        ClaudeCodeSettingsDocument.Parse(null, "settings.json").LocationOf(setting).ShouldBe(location);

    [Fact]
    public void A_new_file_is_written_with_two_space_indentation_and_a_final_newline()
    {
        // Arrange
        var document = ClaudeCodeSettingsDocument.Parse(null, "settings.json");

        // Act
        document.AddEntry(SandboxSetting.AllowWrite, "~/.nuget/packages");
        document.AddEntry(SandboxSetting.AllowedDomains, "api.nuget.org");
        document.SetFlag(SandboxSetting.AllowAllUnixSockets);
        document.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");

        // Assert
        document.Serialize().ShouldBe(
            "{\n"
            + "  \"sandbox\": {\n"
            + "    \"filesystem\": {\n"
            + "      \"allowWrite\": [\n"
            + "        \"~/.nuget/packages\"\n"
            + "      ]\n"
            + "    },\n"
            + "    \"network\": {\n"
            + "      \"allowedDomains\": [\n"
            + "        \"api.nuget.org\"\n"
            + "      ],\n"
            + "      \"allowAllUnixSockets\": true\n"
            + "    }\n"
            + "  },\n"
            + "  \"env\": {\n"
            + "    \"DOTNET_CLI_TELEMETRY_OPTOUT\": \"1\"\n"
            + "  }\n"
            + "}\n");
    }

    [Fact]
    public void An_existing_file_keeps_its_keys_tabs_line_endings_and_missing_final_newline()
    {
        // Arrange
        var document = ClaudeCodeSettingsDocument.Parse("{\r\n\t\"model\": \"x\",\r\n\t\"sandbox\": {}\r\n}", "settings.json");

        // Act
        document.AddEntry(SandboxSetting.ExcludedCommands, "docker *");

        // Assert
        document.Serialize().ShouldBe("{\r\n\t\"model\": \"x\",\r\n\t\"sandbox\": {\r\n\t\t\"excludedCommands\": [\r\n\t\t\t\"docker *\"\r\n\t\t]\r\n\t}\r\n}");
    }

    [Fact]
    public void Entries_are_read_in_file_order_and_removed_by_position()
    {
        // Arrange
        var document = ClaudeCodeSettingsDocument.Parse("""{ "sandbox": { "filesystem": { "denyRead": ["~/.aws", "~/.ssh"] } } }""", "settings.json");

        // Act
        document.RemoveEntry(SandboxSetting.DenyRead, 0);

        // Assert
        document.GetEntries(SandboxSetting.DenyRead).ShouldBe(["~/.ssh"]);
        document.GetEntries(SandboxSetting.DenyWrite).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{ "sandbox": [] }""", SandboxSetting.AllowWrite, "settings.json: $.sandbox must be an object.")]
    [InlineData("""{ "sandbox": { "filesystem": { "allowWrite": "x" } } }""", SandboxSetting.AllowWrite, "settings.json: $.sandbox.filesystem.allowWrite must be a list of strings.")]
    [InlineData("""{ "sandbox": { "excludedCommands": [1] } }""", SandboxSetting.ExcludedCommands, "settings.json: $.sandbox.excludedCommands[0] must be a string of valid Unicode text.")]
    public void A_managed_key_of_the_wrong_type_names_the_file_and_the_JSON_path(string text, SandboxSetting setting, string expected)
    {
        // Arrange
        var document = ClaudeCodeSettingsDocument.Parse(text, "settings.json");

        // Act
        var exception = new Func<object?>(() => document.GetEntries(setting)).ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe(expected);
    }

    [Fact]
    public void Flags_and_variables_tell_absent_from_values_of_another_type()
    {
        // Arrange
        var document = ClaudeCodeSettingsDocument.Parse("""{ "sandbox": { "network": { "allowAllUnixSockets": "yes" } }, "env": { "A": 1, "B": "b", "C": null } }""", "settings.json");

        // Act
        var flag = document.GetFlag(SandboxSetting.AllowAllUnixSockets);
        var hasA = document.TryGetEnvironmentVariable("A", out var a);
        var hasB = document.TryGetEnvironmentVariable("B", out var b);
        var hasC = document.TryGetEnvironmentVariable("C", out _);

        // Assert
        flag.ShouldBeNull();
        ClaudeCodeSettingsDocument.Parse(null, "settings.json").GetFlag(SandboxSetting.AllowAllUnixSockets).ShouldBe(false);
        (hasA, a).ShouldBe((true, null));
        (hasB, b).ShouldBe((true, "b"));
        hasC.ShouldBeFalse();
    }

    [Theory]
    [InlineData("[]", "settings.json: $ must be a JSON object.")]
    [InlineData("""{ "a": 1, "a": 2 }""", "settings.json is not valid JSON")]
    [InlineData("""{ "env": 1 }""", "settings.json: $.env must be an object.")]
    public void Text_that_is_not_a_settings_object_names_the_file(string text, string expected)
    {
        // Act
        var exception = new Func<object?>(() => ClaudeCodeSettingsDocument.Parse(text, "settings.json").TryGetEnvironmentVariable("X", out _)).ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldStartWith(expected, StringComparison.Ordinal);
    }
}
