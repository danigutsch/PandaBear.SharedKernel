namespace PandaBear.Sandbox.Tests;

/// <summary>Tests the format-independent merge against an in-memory document.</summary>
[Trait(TestTraitNames.CapabilityName, "Sandbox")]
public sealed class SettingsMergerTests
{
    [Fact]
    public void Only_missing_entries_are_added_and_paths_ignore_a_trailing_slash_and_domains_ignore_case()
    {
        // Arrange
        var document = new FakeSettingsDocument { Text = "{}" }
            .With(SandboxSetting.AllowWrite, "/tmp/build/", "./hand-added")
            .With(SandboxSetting.AllowedDomains, "API.NUGET.ORG");

        // Act
        var plan = SettingsMerger.Plan(TestSettings.Desired(allowWrite: ["/tmp/build", "./out"], domains: ["api.nuget.org"]), document);

        // Assert
        plan.Changes.ShouldBe([new SettingsChange(SettingsChangeKind.Add, "@AllowWrite", "./out")]);
        document.Entries(SandboxSetting.AllowWrite).ShouldBe(["/tmp/build/", "./hand-added", "./out"]);
        plan.Content.ShouldBe("serialized");
        plan.ExpectedContent.ShouldBe("{}");
    }

    [Fact]
    public void Without_changes_the_content_is_the_original_text_and_nothing_is_serialized()
    {
        // Arrange
        var document = new FakeSettingsDocument().With(SandboxSetting.DenyRead, "~/.ssh");

        // Act
        var plan = SettingsMerger.Plan(TestSettings.Desired(denyRead: ["~/.ssh"]), document);

        // Assert
        plan.Changes.ShouldBeEmpty();
        plan.Content.ShouldBe(string.Empty);
        plan.ExpectedContent.ShouldBeNull();
        document.Serializations.ShouldBe(0);
    }

    [Fact]
    public void Removal_drops_every_spelling_of_the_path_and_nothing_else()
    {
        // Arrange
        var document = new FakeSettingsDocument().With(SandboxSetting.DenyRead, "~/.aws", "~/.ssh", "~/.gnupg", "~/.ssh/");

        // Act
        var plan = SettingsMerger.Plan(TestSettings.Desired(removeDenyRead: ["~/.ssh"]), document);

        // Assert
        document.Entries(SandboxSetting.DenyRead).ShouldBe(["~/.aws", "~/.gnupg"]);
        plan.Changes.Count.ShouldBe(2);
        plan.Changes.ShouldAllSatisfy(static change => change.ShouldBe(new SettingsChange(SettingsChangeKind.Remove, "@DenyRead", "~/.ssh")));
    }

    [Fact]
    public void The_socket_flag_is_set_when_wanted_and_a_set_flag_that_is_not_wanted_is_only_warned_about()
    {
        // Arrange
        var unset = new FakeSettingsDocument();
        var set = new FakeSettingsDocument { Flag = true };

        // Act
        var setPlan = SettingsMerger.Plan(TestSettings.Desired(sockets: true), unset);
        var leftPlan = SettingsMerger.Plan(TestSettings.Desired(), set);

        // Assert
        setPlan.Changes.ShouldBe([new SettingsChange(SettingsChangeKind.Set, "@AllowAllUnixSockets", "true")]);
        unset.Flag.ShouldBe(true);
        leftPlan.Changes.ShouldBeEmpty();
        leftPlan.Warnings.ShouldBe(["@AllowAllUnixSockets is true in the file; the tool does not manage it and leaves it as it is."]);
    }

    [Fact]
    public void A_socket_flag_that_is_not_a_boolean_is_refused_when_wanted_and_otherwise_warned_about()
    {
        // Act
        var refusal = new Func<object?>(() => SettingsMerger.Plan(TestSettings.Desired(sockets: true), new FakeSettingsDocument { Flag = null })).ShouldThrow<InvalidOperationException>();
        var plan = SettingsMerger.Plan(TestSettings.Desired(), new FakeSettingsDocument { Flag = null });

        // Assert
        refusal.Message.ShouldBe("fake.json: @AllowAllUnixSockets must be a boolean.");
        plan.Warnings.ShouldBe(["@AllowAllUnixSockets is not a boolean; the tool leaves it as it is."]);
    }

    [Fact]
    public void Missing_variables_are_added_and_a_different_value_is_kept_and_warned_about()
    {
        // Arrange
        var document = new FakeSettingsDocument().WithVariable("A", "other").WithVariable("B", "1").WithVariable("C", null);

        // Act
        var plan = SettingsMerger.Plan(TestSettings.Desired(environment: new() { ["A"] = "1", ["B"] = "1", ["C"] = "1", ["D"] = "1" }), document);

        // Assert
        plan.Changes.ShouldBe([new SettingsChange(SettingsChangeKind.Add, "@Environment", "D")]);
        plan.Warnings.ShouldBe(["@Environment.A has a different value; the tool leaves it as it is.", "@Environment.C has a different value; the tool leaves it as it is."]);
        document.Variables["A"].ShouldBe("other");
        document.Variables["D"].ShouldBe("1");
    }
}
