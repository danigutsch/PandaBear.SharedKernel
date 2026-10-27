using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using static PandaBear.VaultManager.Tool.Tests.Merge.MergeFixture;

namespace PandaBear.VaultManager.Tool.Tests.Merge;

/// <summary>Tests that <see cref="DecisionsFile"/> serializes decisions without secret values, reads them back, and rejects malformed files.</summary>
public sealed class DecisionsFileTests : IDisposable
{
    private const string OldPassword = "fixture-old-password";
    private const string NewPassword = "fixture-new-password";
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly string directory = Directory.CreateTempSubdirectory("vault-decisions-file-").FullName;

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);

    /// <summary>Group decisions and match overrides survive serialization as version 2 JSON that contains none of the conflicting passwords or notes values.</summary>
    [Fact]
    public void Decisions_round_trip_and_contain_no_values()
    {
        // Arrange
        var keeper = Keeper(Login(OldPassword)) with
        {
            Notes = "fixture-old-notes"
        };
        var bitwarden = Bitwarden(Login(NewPassword)) with
        {
            Notes = "fixture-new-notes"
        };
        var unresolved = Run(new MergePolicy(MergePolicyKind.Ask), keeper, bitwarden).Unresolved;
        var decisions = new MergeDecisions(
            [new GroupDecision(unresolved[0].GroupKey, [.. unresolved.Select(conflict => FieldChoice.Member(conflict.FieldPath, conflict.MemberOriginKeys[0])), FieldChoice.Empty(FieldPaths.LoginTotp)])],
            new MatchOverrides([new OriginKeyPair(KeeperKey, BitwardenKey)], [new OriginKeyPair("a|b|c", "d|e|f")]));

        // Act
        string json = DecisionsFile.Serialize(decisions);
        var reread = DecisionsFile.Deserialize(json);

        // Assert
        json.ShouldContain("\"version\": 2", StringComparison.Ordinal);
        string[] values = [OldPassword, NewPassword, "fixture-old-notes", "fixture-new-notes"];
        values.ShouldAllSatisfy(value => json.ShouldNotContain(value, StringComparison.Ordinal));
        var group = reread.Groups.ShouldHaveSingleItem();
        group.GroupKey.ShouldBe(unresolved[0].GroupKey);
        group.Choices.ShouldBe(decisions.Groups[0].Choices);
        reread.Overrides.Links.ShouldBe(decisions.Overrides.Links);
        reread.Overrides.Splits.ShouldBe(decisions.Overrides.Splits);
    }

    /// <summary>Notes decisions, including a Share entry with its name, position, and hash, round trip, while a file with no notes section reads as an empty list and serializing no notes omits the section.</summary>
    [Fact]
    public void Notes_decisions_round_trip_and_files_without_them_still_read()
    {
        // Arrange
        var decisions = MergeDecisions.None with
        {
            Notes = [new ItemNotesDecision("a|b|c", NotesDecision.Share, "GitHub", 2, ItemNotesDecision.HashOf("shared notes")), new ItemNotesDecision("a|b|d", NotesDecision.Hidden)]
        };

        // Act
        string json = DecisionsFile.Serialize(decisions);
        var reread = DecisionsFile.Deserialize(json);
        string withoutNotesJson = DecisionsFile.Serialize(MergeDecisions.None);
        var withoutNotes = DecisionsFile.Deserialize("""{"version":2,"groups":[],"links":[],"splits":[]}""");

        // Assert
        reread.Notes.ShouldBe(decisions.Notes);
        json.ShouldContain("\"decision\": \"Share\"", StringComparison.Ordinal);
        withoutNotesJson.ShouldNotContain("notes", StringComparison.Ordinal);
        withoutNotes.Notes.ShouldBeEmpty();
    }

    /// <summary>Deserializing a file with two notes entries for the same origin key throws <see cref="InvalidDataException"/> mentioning "two notes entries".</summary>
    [Fact]
    public void Notes_decisions_reject_two_entries_for_one_item()
    {
        // Arrange
        const string json = """{"version":2,"groups":[],"links":[],"splits":[],"notes":[{"originKey":"a|b|c","decision":"Share"},{"originKey":"a|b|c","decision":"Private"}]}""";

        var action = () => DecisionsFile.Deserialize(json);

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldContain("two notes entries", StringComparison.Ordinal);
    }

    /// <summary>WithNotes replaces entries for items it names, drops pending entries for items it omits, keeps non-pending entries, and FindNotes returns Pending for an unknown item.</summary>
    [Fact]
    public void Notes_updates_replace_entries_and_drop_stale_pending_ones()
    {
        // Arrange
        var decisions = MergeDecisions.None with
        {
            Notes = [new ItemNotesDecision("a|b|c", NotesDecision.Private), new ItemNotesDecision("a|b|old", NotesDecision.Pending), new ItemNotesDecision("a|b|e", NotesDecision.Share)],
        };

        // Act
        var updated = decisions.WithNotes([new ItemNotesDecision("a|b|e", NotesDecision.Pending, "Changed"), new ItemNotesDecision("a|b|d", NotesDecision.Pending, "Two", 1)]);

        // Assert
        updated.Notes.ShouldBe([new ItemNotesDecision("a|b|c", NotesDecision.Private), new ItemNotesDecision("a|b|e", NotesDecision.Pending, "Changed"), new ItemNotesDecision("a|b|d", NotesDecision.Pending, "Two", 1)]);
        updated.FindNotes("a|b|c").ShouldBe(NotesDecision.Private);
        updated.FindNotes("a|b|f").ShouldBe(NotesDecision.Pending);
    }

    /// <summary>Accepted and rejected finding choices for two items round trip and can be looked up by origin key and finding ID, while the JSON omits secret values and an unknown pair yields null.</summary>
    [Fact]
    public void Decisions_with_finding_decisions_round_trip_without_values()
    {
        // Arrange
        const string NotesPassword = "fixture-notes-password";
        var decisions = new MergeDecisions([], MatchOverrides.None)
        {
            Findings =
            [
                new ItemFindingDecisions(KeeperKey, [new FindingChoice("notes-password@notes:42", FindingDecision.Accepted), new FindingChoice("recovery-code-block@notes:0", FindingDecision.Rejected)]),
                new ItemFindingDecisions(BitwardenKey, [new FindingChoice("card-number@fields[0].value:0", FindingDecision.Rejected)]),
            ],
        };

        // Act
        string json = DecisionsFile.Serialize(decisions);
        var reread = DecisionsFile.Deserialize(json);

        // Assert
        json.ShouldContain("\"version\": 2", StringComparison.Ordinal);
        json.ShouldContain("\"findings\"", StringComparison.Ordinal);
        json.ShouldNotContain(NotesPassword, StringComparison.Ordinal);
        reread.Findings.Count.ShouldBe(2);
        reread.Findings[0].OriginKey.ShouldBe(KeeperKey);
        reread.Findings[0].Choices.ShouldBe(decisions.Findings[0].Choices);
        reread.Findings[1].Choices.ShouldBe(decisions.Findings[1].Choices);
        reread.FindFinding(KeeperKey, "notes-password@notes:42").ShouldBe(FindingDecision.Accepted);
        reread.FindFinding(BitwardenKey, "card-number@fields[0].value:0").ShouldBe(FindingDecision.Rejected);
        reread.FindFinding(BitwardenKey, "notes-password@notes:42").ShouldBeNull();
    }

    /// <summary>The serialized JSON spells out the Member, Empty, and Pending choice kinds and the Accepted, Rejected, and Pending finding decisions by name.</summary>
    [Fact]
    public void Decisions_name_choice_kinds_and_finding_decisions_in_the_file()
    {
        // Arrange
        var decisions = new MergeDecisions(
            [new GroupDecision("0123456789abcdef", [FieldChoice.Member(FieldPaths.Notes, KeeperKey), FieldChoice.Empty(FieldPaths.LoginTotp), FieldChoice.Pending(FieldPaths.LoginPassword, [])])],
            MatchOverrides.None)
        {
            Findings = [new ItemFindingDecisions(KeeperKey, [new FindingChoice("a@notes:0", FindingDecision.Accepted), new FindingChoice("b@notes:9", FindingDecision.Rejected), new FindingChoice("c@notes:20", FindingDecision.Pending)])],
        };

        // Act
        string json = DecisionsFile.Serialize(decisions);

        // Assert
        string[] names = ["\"kind\": \"Member\"", "\"kind\": \"Empty\"", "\"kind\": \"Pending\"", "\"decision\": \"Accepted\"", "\"decision\": \"Rejected\"", "\"decision\": \"Pending\""];
        names.ShouldAllSatisfy(name => json.ShouldContain(name, StringComparison.Ordinal));
    }

    /// <summary>A pending field choice with its options and a pending finding round trip without password values, Find and FindFinding report no decision for them, and rerunning the merge still leaves the conflict unresolved.</summary>
    [Fact]
    public void Pending_entries_round_trip_without_values_and_load_as_no_decision()
    {
        // Arrange
        var keeper = WithoutTimestamp(Keeper(Login(OldPassword)));
        var bitwarden = WithoutTimestamp(Bitwarden(Login(NewPassword)));
        var conflict = Run(keeper, bitwarden).Unresolved.ShouldHaveSingleItem();
        var options = conflict.MemberOriginKeys.Select((key, index) => new FieldChoiceOption(key, conflict.MemberNames[index])).ToArray();
        var decisions = new MergeDecisions([new GroupDecision(conflict.GroupKey, [FieldChoice.Pending(conflict.FieldPath, options)])], MatchOverrides.None)
        {
            Findings = [new ItemFindingDecisions(KeeperKey, [new FindingChoice("notes-password@notes:42", FindingDecision.Pending)], "Example")],
        };

        // Act
        string json = DecisionsFile.Serialize(decisions);
        var reread = DecisionsFile.Deserialize(json);
        var rerun = Run(new MergePolicy(), reread, null, keeper, bitwarden);

        // Assert
        json.ShouldNotContain(OldPassword, StringComparison.Ordinal);
        json.ShouldNotContain(NewPassword, StringComparison.Ordinal);
        var choice = reread.Groups.ShouldHaveSingleItem().Choices.ShouldHaveSingleItem();
        choice.Kind.ShouldBe(FieldChoiceKind.Pending);
        choice.OriginKey.ShouldBeNull();
        choice.Options.ShouldNotBeNull().ShouldBe(options);
        reread.Findings.ShouldHaveSingleItem().ItemName.ShouldBe("Example");
        reread.Find(conflict.GroupKey, conflict.FieldPath).ShouldBeNull();
        reread.FindFinding(KeeperKey, "notes-password@notes:42").ShouldBeNull();
        rerun.Unresolved.ShouldHaveSingleItem().FieldPath.ShouldBe(conflict.FieldPath);
    }

    /// <summary>A version 2 file with no findings section loads with an empty findings list.</summary>
    [Fact]
    public void Decisions_written_without_a_findings_section_still_load()
    {
        // Act
        var decisions = DecisionsFile.Deserialize("""{"version":2,"groups":[],"links":[],"splits":[]}""");

        // Assert
        decisions.Findings.ShouldBeEmpty();
    }

    /// <summary>Lowercase "member" and "rejected" names in a hand-edited file load as the Member field choice and Rejected finding decision.</summary>
    [Fact]
    public void Decisions_edited_by_hand_with_lowercase_names_load()
    {
        // Act
        var decisions = DecisionsFile.Deserialize("""
            {"version":2,"groups":[{"groupKey":"g","choices":[{"fieldPath":"notes","kind":"member","originKey":"k"}]}],"links":[],"splits":[],
             "findings":[{"originKey":"k","choices":[{"findingId":"f","decision":"rejected"}]}]}
            """);

        // Assert
        decisions.FindFinding("k", "f").ShouldBe(FindingDecision.Rejected);
        decisions.Find("g", "notes").ShouldBe(FieldChoice.Member("notes", "k"));
    }

    /// <summary>Finding entries with a blank origin key, an empty finding ID, a numeric or missing decision, or null choices make deserialization throw <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("""{"version":2,"groups":[],"links":[],"splits":[],"findings":[{"originKey":" ","choices":[]}]}""")]
    [InlineData("""{"version":2,"groups":[],"links":[],"splits":[],"findings":[{"originKey":"k","choices":[{"findingId":"","decision":"accepted"}]}]}""")]
    [InlineData("""{"version":2,"groups":[],"links":[],"splits":[],"findings":[{"originKey":"k","choices":[{"findingId":"f","decision":7}]}]}""")]
    [InlineData("""{"version":2,"groups":[],"links":[],"splits":[],"findings":[{"originKey":"k","choices":[{"findingId":"f"}]}]}""")]
    [InlineData("""{"version":2,"groups":[],"links":[],"splits":[],"findings":[{"originKey":"k","choices":null}]}""")]
    public void Decisions_with_incomplete_finding_decisions_are_rejected(string json)
    {
        // Arrange
        var action = () => DecisionsFile.Deserialize(json);

        // Act, Assert
        action.ShouldThrow<InvalidDataException>();
    }

    /// <summary>Files declaring version 1 or 3 are rejected with an <see cref="InvalidDataException"/> that names the supported "version 2".</summary>
    [Theory]
    [InlineData("""{"version":1,"groups":[],"links":[],"splits":[]}""")]
    [InlineData("""{"version":3,"groups":[],"links":[],"splits":[]}""")]
    public void Decisions_with_unsupported_version_are_rejected(string json)
    {
        // Arrange
        var action = () => DecisionsFile.Deserialize(json);

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldContain("version 2", StringComparison.Ordinal);
    }

    /// <summary>Field choices that lack the origin key of a Member kind, carry a numeric kind, or omit the kind make deserialization throw <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("""{"version":2,"groups":[{"groupKey":"g","choices":[{"fieldPath":"notes","kind":"member"}]}],"links":[],"splits":[]}""")]
    [InlineData("""{"version":2,"groups":[{"groupKey":"g","choices":[{"fieldPath":"notes","kind":7}]}],"links":[],"splits":[]}""")]
    [InlineData("""{"version":2,"groups":[{"groupKey":"g","choices":[{"fieldPath":"notes"}]}],"links":[],"splits":[]}""")]
    public void Decisions_with_incomplete_field_choices_are_rejected(string json)
    {
        // Arrange
        var action = () => DecisionsFile.Deserialize(json);

        // Act, Assert
        action.ShouldThrow<InvalidDataException>();
    }

    /// <summary>A property the format does not define, at the top level or nested in groups, options, links, or findings, is rejected with a message containing its JSON path.</summary>
    [Theory]
    [InlineData("""{"version":2,"groups":[],"links":[],"splits":[],"finding":[{"originKey":"k","choices":[{"findingId":"f","decision":"Rejected"}]}]}""", "$.finding")]
    [InlineData("""{"version":2,"groups":[{"groupKey":"g","choices":[{"fieldPath":"notes","kind":"Member","origin":"k"}]}],"links":[],"splits":[]}""", "$.groups[0].choices[0].origin")]
    [InlineData("""{"version":2,"groups":[{"groupKey":"g","choices":[{"fieldPath":"notes","kind":"Pending","options":[{"originKey":"k","name":"n"}]}]}],"links":[],"splits":[]}""", "$.groups[0].choices[0].options[0].name")]
    [InlineData("""{"version":2,"groups":[],"links":[{"first":"a","second":"b","third":"c"}],"splits":[]}""", "$.links[0].third")]
    [InlineData("""{"version":2,"groups":[],"links":[],"splits":[],"findings":[{"originKey":"k","choices":[{"findingId":"f","decision":"Rejected","reason":"r"}]}]}""", "$.findings[0].choices[0].reason")]
    public void Decisions_with_an_unknown_property_are_rejected_with_its_json_path(string json, string jsonPath)
    {
        // Arrange
        var action = () => DecisionsFile.Deserialize(json);

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldContain(" " + jsonPath + " ", StringComparison.Ordinal);
    }

    /// <summary>A property that appears twice, including with different casing, is rejected with a message containing its JSON path.</summary>
    [Theory]
    [InlineData("""{"version":2,"groups":[],"links":[],"splits":[],"findings":[{"originKey":"k","choices":[{"findingId":"f","decision":"Rejected"}]}],"findings":[]}""", "$.findings")]
    [InlineData("""{"version":2,"groups":[],"links":[],"splits":[],"findings":[{"originKey":"k","choices":[{"findingId":"f","decision":"Rejected"}]}],"Findings":[]}""", "$.Findings")]
    [InlineData("""{"version":2,"groups":[{"groupKey":"g","choices":[{"fieldPath":"notes","kind":"Empty"}]}],"links":[],"splits":[],"groups":[]}""", "$.groups")]
    [InlineData("""{"version":2,"groups":[],"links":[{"first":"a","second":"b"}],"splits":[],"links":[]}""", "$.links")]
    [InlineData("""{"version":2,"groups":[],"links":[],"splits":[{"first":"a","second":"b"}],"splits":[]}""", "$.splits")]
    [InlineData("""{"version":2,"groups":[],"links":[],"splits":[],"findings":[{"originKey":"k","choices":[{"findingId":"f","decision":"Rejected","decision":"Accepted"}]}]}""", "$.findings[0].choices[0].decision")]
    [InlineData("""{"version":2,"groups":[{"groupKey":"g","choices":[{"fieldPath":"notes","kind":"Member","originKey":"a","originKey":"b"}]}],"links":[],"splits":[]}""", "$.groups[0].choices[0].originKey")]
    public void Decisions_with_a_repeated_property_are_rejected_with_its_json_path(string json, string jsonPath)
    {
        // Arrange
        var action = () => DecisionsFile.Deserialize(json);

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldContain(" " + jsonPath + " ", StringComparison.Ordinal);
    }

    /// <summary>Reading a file with an unknown property throws an error naming the file path and "$.finding" but never echoing the property's value.</summary>
    [Fact]
    public void Read_names_the_file_and_the_json_path_of_an_unknown_property_without_its_value()
    {
        // Arrange
        const string Value = "fixture-value-in-an-unknown-property";
        string path = Path.Combine(directory, "decisions.json");
        File.WriteAllText(path, $$"""{"version":2,"groups":[],"links":[],"splits":[],"finding":"{{Value}}"}""");

        var action = () => DecisionsFile.Read(path);

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        string message = exception.Message;
        message.ShouldContain(path, StringComparison.Ordinal);
        message.ShouldContain("$.finding", StringComparison.Ordinal);
        message.ShouldNotContain(Value, StringComparison.Ordinal);
    }

    /// <summary>An item name with an apostrophe and accent is written as typed, a name with tab and escape control characters is escaped, and both read back exactly.</summary>
    [Fact]
    public void Decisions_file_writes_item_names_as_typed_and_reads_them_back_exactly()
    {
        // Arrange
        const string Name = "Bob's Café Bank";
        string controlName = string.Concat("Tab", (char)0x09, "and", (char)0x1B, "escape");
        var decisions = new MergeDecisions(
            [new GroupDecision("0123456789abcdef", [FieldChoice.Pending(FieldPaths.LoginPassword, [new FieldChoiceOption(KeeperKey, Name), new FieldChoiceOption(BitwardenKey, controlName)])])],
            MatchOverrides.None)
        {
            Findings = [new ItemFindingDecisions(KeeperKey, [new FindingChoice("a@notes:0", FindingDecision.Pending)], Name)],
        };

        // Act
        string json = DecisionsFile.Serialize(decisions);
        var reread = DecisionsFile.Deserialize(json);

        // Assert
        json.ShouldContain("\"itemName\": \"Bob's Café Bank\"", StringComparison.Ordinal);
        json.ShouldNotContain(controlName, StringComparison.Ordinal);
        reread.Findings.ShouldHaveSingleItem().ItemName.ShouldBe(Name);
        var options = reread.Groups.ShouldHaveSingleItem().Choices.ShouldHaveSingleItem().Options.ShouldNotBeNull();
        options.Select(option => option.ItemName).ShouldBe([Name, controlName]);
    }

    /// <summary>Right-to-left override, isolate, zero-width, soft-hyphen, and tag characters in an item name are written as <c>\u</c> escapes, never raw, and read back as the original name.</summary>
    [Fact]
    public void Decisions_file_escapes_format_characters_in_item_names_and_reads_them_back_exactly()
    {
        // Arrange
        // A right-to-left override, an isolate, invisible spaces, and a tag character, which an editor applies or hides
        // instead of showing.
        string name = string.Concat("Pay", (char)0x202E, "knab", (char)0x2066, "x", (char)0x200B, (char)0x00AD, char.ConvertFromUtf32(0xE0041));
        var decisions = new MergeDecisions([], MatchOverrides.None)
        {
            Findings = [new ItemFindingDecisions(KeeperKey, [new FindingChoice("a@notes:0", FindingDecision.Pending)], name)],
        };

        // Act
        string json = DecisionsFile.Serialize(decisions);
        var reread = DecisionsFile.Deserialize(json);

        // Assert
        json.ShouldContain("\"itemName\": \"Pay\\u202Eknab\\u2066x\\u200B\\u00AD\\uDB40\\uDC41\"", StringComparison.Ordinal);
        char[] formatCharacters = [(char)0x202E, (char)0x2066, (char)0x200B, (char)0x00AD];
        formatCharacters.ShouldAllSatisfy(character => json.ShouldNotContain(character));
        reread.Findings.ShouldHaveSingleItem().ItemName.ShouldBe(name);
    }

    /// <summary>Reading a path with no file returns the shared <see cref="MergeDecisions.None"/> instance and does not create the file.</summary>
    [Fact]
    public void Read_returns_no_decisions_when_the_file_does_not_exist()
    {
        // Arrange
        string path = Path.Combine(directory, "decisions.json");

        // Act
        var decisions = DecisionsFile.Read(path);

        // Assert
        decisions.ShouldBeSameAs(MergeDecisions.None);
        File.Exists(path).ShouldBeFalse();
    }

    /// <summary>Writing over an existing group-readable file replaces its content, leaves it with owner-only permissions on Unix, and Read loads the written group decision.</summary>
    [Fact]
    public void Write_replaces_the_file_readable_only_by_its_owner_and_read_loads_it()
    {
        // Arrange
        string path = Path.Combine(directory, "decisions.json");
        File.WriteAllText(path, "{}");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, OwnerOnly | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }

        var decisions = Decide("0123456789abcdef", FieldChoice.Empty(FieldPaths.Notes));

        // Act
        DecisionsFile.Write(path, decisions);
        var reread = DecisionsFile.Read(path);

        // Assert
        reread.Groups.ShouldHaveSingleItem().Choices.ShouldBe(decisions.Groups[0].Choices);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(path).ShouldBe(OwnerOnly);
        }
    }
}
