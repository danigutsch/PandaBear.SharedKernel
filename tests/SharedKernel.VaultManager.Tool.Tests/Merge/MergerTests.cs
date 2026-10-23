using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using static PandaBear.VaultManager.Tool.Tests.Merge.MergeFixture;

namespace PandaBear.VaultManager.Tool.Tests.Merge;

public sealed class MergerTests
{
    private const string OldPassword = "fixture-old-password";
    private const string NewPassword = "fixture-new-password";
    private const string KeeperTotp = "JBSWY3DPEHPK3PXP";
    private const string BitwardenTotp = "KRSXG5CTMVRXEZLU";

    [Fact]
    public void Merge_newer_duplicate_without_totp_keeps_older_totp()
    {
        var keeper = Keeper(Login(OldPassword, KeeperTotp));
        var bitwarden = Bitwarden(Login(NewPassword));

        var result = Run(keeper, bitwarden);

        var item = result.Merged.Items.ShouldHaveSingleItem();
        var login = item.Login.ShouldNotBeNull();
        login.Password.ShouldBe(NewPassword);
        login.Totp.ShouldBe(KeeperTotp);
        item.PasswordHistory.ShouldBe([new PasswordHistoryEntry(OldPassword, Older)]);
        result.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_older_duplicate_without_totp_keeps_newer_totp()
    {
        var keeper = Keeper(Login(OldPassword));
        var bitwarden = Bitwarden(Login(NewPassword, BitwardenTotp));

        var result = Run(keeper, bitwarden);

        var login = result.Merged.Items.ShouldHaveSingleItem().Login.ShouldNotBeNull();
        login.Totp.ShouldBe(BitwardenTotp);
        result.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_both_totp_different_keeps_newer_and_preserves_older_as_hidden_field()
    {
        var keeper = Keeper(Login(NewPassword, KeeperTotp));
        var bitwarden = Bitwarden(Login(NewPassword, BitwardenTotp));

        var result = Run(keeper, bitwarden);

        var item = result.Merged.Items.ShouldHaveSingleItem();
        item.Login.ShouldNotBeNull().Totp.ShouldBe(BitwardenTotp);
        item.Fields.ShouldContain(new CustomField("TOTP (from keeper.json)", KeeperTotp, CustomFieldType.Hidden));
        result.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_newer_password_moves_older_password_to_history_with_its_timestamp()
    {
        var keeper = Keeper(Login(OldPassword));
        var bitwarden = Bitwarden(Login(NewPassword));

        var item = Run(keeper, bitwarden).Merged.Items.ShouldHaveSingleItem();

        item.Login.ShouldNotBeNull().Password.ShouldBe(NewPassword);
        item.PasswordHistory.ShouldHaveSingleItem().ShouldBe(new PasswordHistoryEntry(OldPassword, Older));
    }

    [Fact]
    public void Merge_equal_passwords_add_no_history_entry()
    {
        var keeper = Keeper(Login(NewPassword, KeeperTotp));
        var bitwarden = Bitwarden(Login(NewPassword));

        var item = Run(keeper, bitwarden).Merged.Items.ShouldHaveSingleItem();

        item.PasswordHistory.ShouldBeEmpty();
        item.Fields.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_older_only_username_is_kept_when_newer_username_is_empty()
    {
        var keeper = Keeper(Login(NewPassword));
        var bitwarden = Bitwarden(Login(NewPassword, username: ""));

        var result = Run(new MergePolicy(), null, LinkKeeperAndBitwarden(), keeper, bitwarden);

        result.Merged.Items.ShouldHaveSingleItem().Login.ShouldNotBeNull().Username.ShouldBe(Username);
        result.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_whitespace_only_value_counts_as_absent()
    {
        var keeper = Keeper(Login(OldPassword) with
        {
            Totp = KeeperTotp
        });
        var bitwarden = Bitwarden(Login("   ", " \t "));

        var result = Run(keeper, bitwarden);

        var item = result.Merged.Items.ShouldHaveSingleItem();
        var login = item.Login.ShouldNotBeNull();
        login.Password.ShouldBe(OldPassword);
        login.Totp.ShouldBe(KeeperTotp);
        item.PasswordHistory.ShouldBeEmpty();
        item.Fields.ShouldBeEmpty();
        result.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_uris_are_unioned_without_normalized_duplicates()
    {
        var keeper = Keeper(Login(NewPassword) with
        {
            Uris = [new LoginUri(ExampleUri), new LoginUri("https://k-only.example.com")]
        });
        var bitwarden = Bitwarden(Login(NewPassword) with
        {
            Uris = [new LoginUri("HTTPS://EXAMPLE.com/login/"), new LoginUri("androidapp://com.example")]
        });

        var login = Run(keeper, bitwarden).Merged.Items.ShouldHaveSingleItem().Login.ShouldNotBeNull();

        login.Uris.Select(uri => uri.Value).ShouldBe(["HTTPS://EXAMPLE.com/login/", "androidapp://com.example", "https://k-only.example.com"]);
    }

    [Fact]
    public void Merge_custom_fields_are_unioned_and_identical_fields_are_not_duplicated()
    {
        var keeper = Keeper(Login(NewPassword)) with
        {
            Fields = [new CustomField("PIN", "1234", CustomFieldType.Hidden), new CustomField("Shared", "line one\r\nline two", CustomFieldType.Text)],
        };
        var bitwarden = Bitwarden(Login(NewPassword)) with
        {
            Fields = [new CustomField("Shared", "line one\nline two", CustomFieldType.Text), new CustomField("Recovery", "r-1", CustomFieldType.Hidden)],
        };

        var item = Run(keeper, bitwarden).Merged.Items.ShouldHaveSingleItem();

        item.Fields.Select(field => field.Name).ShouldBe(["Shared", "Recovery", "PIN"]);
    }

    [Fact]
    public void Merge_fields_differing_only_in_type_are_kept_once_with_the_stricter_type()
    {
        var keeper = Keeper(Login(NewPassword)) with
        {
            Fields =
            [
                new CustomField("Account Number", "12345678", CustomFieldType.Hidden),
                new CustomField("Remember", "true", CustomFieldType.Boolean),
            ],
        };
        var bitwarden = Bitwarden(Login(NewPassword)) with
        {
            Fields =
            [
                new CustomField("Account Number", "12345678", CustomFieldType.Text),
                new CustomField("Remember", "true", CustomFieldType.Text),
            ],
        };

        var item = Run(keeper, bitwarden).Merged.Items.ShouldHaveSingleItem();

        item.Fields.ShouldBeEquivalentTo(
            new CustomField("Account Number", "12345678", CustomFieldType.Hidden),
            new CustomField("Remember", "true", CustomFieldType.Boolean));
    }

    [Fact]
    public void Merge_passkeys_from_both_sides_are_kept()
    {
        var keeperPasskey = new Fido2Credential { CredentialId = "cred-k", KeyValue = "key-k", RpId = "example.com" };
        var bitwardenPasskey = new Fido2Credential { CredentialId = "cred-b", KeyValue = "key-b", RpId = "example.com" };
        var keeper = Keeper(Login(NewPassword) with
        {
            Fido2Credentials = [keeperPasskey]
        });
        var bitwarden = Bitwarden(Login(NewPassword) with
        {
            Fido2Credentials = [bitwardenPasskey, keeperPasskey]
        });

        var login = Run(keeper, bitwarden).Merged.Items.ShouldHaveSingleItem().Login.ShouldNotBeNull();

        login.Fido2Credentials.Select(passkey => passkey.CredentialId).ShouldBe(["cred-b", "cred-k"]);
    }

    [Fact]
    public void Merge_password_histories_are_unioned_newest_first()
    {
        var keeper = Keeper(Login(NewPassword)) with
        {
            PasswordHistory = [new PasswordHistoryEntry("h1", new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero)), new PasswordHistoryEntry("h2", new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero))],
        };
        var bitwarden = Bitwarden(Login(NewPassword)) with
        {
            PasswordHistory = [new PasswordHistoryEntry("h3", new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero)), new PasswordHistoryEntry("h1", new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero))],
        };

        var item = Run(keeper, bitwarden).Merged.Items.ShouldHaveSingleItem();

        item.PasswordHistory.Select(entry => entry.Password).ShouldBe(["h3", "h1", "h2"]);
    }

    [Fact]
    public void Merge_notes_conflict_keeps_newer_notes_and_older_notes_as_hidden_field()
    {
        var keeper = Keeper(Login(NewPassword)) with
        {
            Notes = "older notes"
        };
        var bitwarden = Bitwarden(Login(NewPassword)) with
        {
            Notes = "newer notes"
        };

        var item = Run(keeper, bitwarden).Merged.Items.ShouldHaveSingleItem();

        item.Notes.ShouldBe("newer notes");
        item.Fields.ShouldContain(new CustomField("Notes (from keeper.json)", "older notes", CustomFieldType.Hidden));
    }

    [Fact]
    public void Merge_long_losing_notes_are_appended_to_winner_notes()
    {
        string longNotes = new('x', 4_000);
        var keeper = Keeper(Login(NewPassword)) with
        {
            Notes = longNotes
        };
        var bitwarden = Bitwarden(Login(NewPassword)) with
        {
            Notes = "newer notes"
        };

        var item = Run(keeper, bitwarden).Merged.Items.ShouldHaveSingleItem();

        item.Notes.ShouldBe("newer notes\n\n--- Notes from keeper.json ---\n" + longNotes);
        item.Fields.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_card_number_present_only_in_older_card_is_kept()
    {
        var older = Card(new ItemOrigin("keeper", KeeperFile, "k1", Older), Older, new CardData { Brand = "Visa", Number = "4111111111111111" });
        var newer = Card(new ItemOrigin("bitwarden", BitwardenFile, "b1", null), Newer, new CardData { Brand = "Visa", CardholderName = "Alice Example" });

        var result = Run(older, newer);

        var card = result.Merged.Items.ShouldHaveSingleItem().Card.ShouldNotBeNull();
        card.Number.ShouldBe("4111111111111111");
        card.CardholderName.ShouldBe("Alice Example");
        result.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_reprompt_and_favorite_keep_stricter_or_true_value()
    {
        var keeper = Keeper(Login(NewPassword)) with
        {
            Reprompt = true
        };
        var bitwarden = Bitwarden(Login(NewPassword)) with
        {
            Favorite = true
        };

        var item = Run(keeper, bitwarden).Merged.Items.ShouldHaveSingleItem();

        item.Reprompt.ShouldBeTrue();
        item.Favorite.ShouldBeTrue();
    }

    [Fact]
    public void Merge_conflict_without_timestamps_is_unresolved_under_newest_policy()
    {
        var keeper = WithoutTimestamp(Keeper(Login(OldPassword)));
        var bitwarden = WithoutTimestamp(Bitwarden(Login(NewPassword)));

        var result = Run(keeper, bitwarden);

        var conflict = result.Unresolved.ShouldHaveSingleItem();
        conflict.FieldPath.ShouldBe(FieldPaths.LoginPassword);
        conflict.MemberOriginKeys.ShouldBe([KeeperKey, BitwardenKey]);
        conflict.MemberNames.ShouldBe(["Example", "Example"]);
        var item = result.Merged.Items.ShouldHaveSingleItem();
        item.Login.ShouldNotBeNull().Password.ShouldBe(OldPassword);
        item.PasswordHistory.ShouldBe([new PasswordHistoryEntry(NewPassword, Now)]);
    }

    [Fact]
    public void Merge_one_sided_value_without_timestamps_is_still_resolved()
    {
        var keeper = WithoutTimestamp(Keeper(Login(NewPassword, KeeperTotp)));
        var bitwarden = WithoutTimestamp(Bitwarden(Login(NewPassword)));

        var result = Run(keeper, bitwarden);

        result.Merged.Items.ShouldHaveSingleItem().Login.ShouldNotBeNull().Totp.ShouldBe(KeeperTotp);
        result.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_ask_policy_leaves_every_real_conflict_unresolved_but_fills_one_sided_values()
    {
        var keeper = Keeper(Login(OldPassword, KeeperTotp)) with
        {
            Notes = "older notes"
        };
        var bitwarden = Bitwarden(Login(NewPassword)) with
        {
            Notes = "newer notes"
        };

        var result = Run(new MergePolicy(MergePolicyKind.Ask), keeper, bitwarden);

        result.Unresolved.Select(conflict => conflict.FieldPath).ShouldBe([FieldPaths.Notes, FieldPaths.LoginPassword]);
        var item = result.Merged.Items.ShouldHaveSingleItem();
        var login = item.Login.ShouldNotBeNull();
        login.Totp.ShouldBe(KeeperTotp);
        login.Password.ShouldBe(NewPassword);
        item.PasswordHistory.ShouldBe([new PasswordHistoryEntry(OldPassword, Older)]);
    }

    [Fact]
    public void Merge_keeps_a_one_sided_folder_when_the_newer_member_has_none()
    {
        var keeper = Keeper(Login(NewPassword)) with
        {
            FolderPath = "Family/Finance/Shopping"
        };
        var bitwarden = Bitwarden(Login(NewPassword));

        var result = Run(keeper, bitwarden);

        result.Merged.Items.ShouldHaveSingleItem().FolderPath.ShouldBe("Family/Finance/Shopping");
        result.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_folder_conflict_without_timestamps_is_unresolved_under_newest_policy()
    {
        var keeper = WithoutTimestamp(Keeper(Login(NewPassword))) with
        {
            FolderPath = "Finance"
        };
        var bitwarden = WithoutTimestamp(Bitwarden(Login(NewPassword))) with
        {
            FolderPath = "Imported"
        };

        var result = Run(keeper, bitwarden);

        var conflict = result.Unresolved.ShouldHaveSingleItem();
        conflict.FieldPath.ShouldBe(FieldPaths.Folder);
        conflict.MemberOriginKeys.ShouldBe([KeeperKey, BitwardenKey]);
    }

    [Fact]
    public void Merge_ask_policy_leaves_a_folder_conflict_unresolved()
    {
        var keeper = Keeper(Login(NewPassword)) with
        {
            FolderPath = "Finance"
        };
        var bitwarden = Bitwarden(Login(NewPassword)) with
        {
            FolderPath = "Imported"
        };

        var result = Run(new MergePolicy(MergePolicyKind.Ask), keeper, bitwarden);

        result.Unresolved.ShouldHaveSingleItem().FieldPath.ShouldBe(FieldPaths.Folder);
    }

    [Fact]
    public void Merge_folder_decision_takes_the_chosen_member_folder()
    {
        var keeper = WithoutTimestamp(Keeper(Login(NewPassword))) with
        {
            FolderPath = "Finance"
        };
        var bitwarden = WithoutTimestamp(Bitwarden(Login(NewPassword))) with
        {
            FolderPath = "Imported"
        };
        string groupKey = Run(keeper, bitwarden).Unresolved.ShouldHaveSingleItem().GroupKey;
        var decisions = Decide(groupKey, FieldChoice.Member(FieldPaths.Folder, KeeperKey));

        var result = Run(new MergePolicy(), decisions, null, keeper, bitwarden);

        result.Merged.Items.ShouldHaveSingleItem().FolderPath.ShouldBe("Finance");
        result.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_prefer_source_policy_picks_source_and_preserves_loser()
    {
        var keeper = Keeper(Login(OldPassword));
        var bitwarden = Bitwarden(Login(NewPassword));

        var result = Run(new MergePolicy(MergePolicyKind.PreferSource, KeeperFile), keeper, bitwarden);

        var item = result.Merged.Items.ShouldHaveSingleItem();
        item.Login.ShouldNotBeNull().Password.ShouldBe(OldPassword);
        item.PasswordHistory.ShouldBe([new PasswordHistoryEntry(NewPassword, Newer)]);
        result.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_decision_choosing_empty_drops_value_from_result()
    {
        var keeper = Keeper(Login(NewPassword, KeeperTotp));
        var bitwarden = Bitwarden(Login(NewPassword));
        var groupKey = Match(null, keeper, bitwarden).Groups.ShouldHaveSingleItem().Key;
        var decisions = Decide(groupKey, FieldChoice.Empty(FieldPaths.LoginTotp));

        var result = Run(new MergePolicy(), decisions, null, keeper, bitwarden);

        var item = result.Merged.Items.ShouldHaveSingleItem();
        item.Login.ShouldNotBeNull().Totp.ShouldBeNull();
        item.Fields.ShouldBeEmpty();
        result.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_decision_choosing_older_side_overrides_newest_policy()
    {
        var keeper = Keeper(Login(OldPassword));
        var bitwarden = Bitwarden(Login(NewPassword));
        var groupKey = Match(null, keeper, bitwarden).Groups.ShouldHaveSingleItem().Key;
        var decisions = Decide(groupKey, FieldChoice.Member(FieldPaths.LoginPassword, KeeperKey));

        var result = Run(new MergePolicy(), decisions, null, keeper, bitwarden);

        var item = result.Merged.Items.ShouldHaveSingleItem();
        item.Login.ShouldNotBeNull().Password.ShouldBe(OldPassword);
        item.PasswordHistory.ShouldBe([new PasswordHistoryEntry(NewPassword, Newer)]);
        result.Report.Groups.ShouldHaveSingleItem().Fields.ShouldContain(new FieldResolution(FieldPaths.LoginPassword, ResolutionKind.Decision));
    }

    [Fact]
    public void Merge_three_way_group_applies_rules_across_all_members()
    {
        var keeper = Keeper(Login("p1", KeeperTotp));
        var middle = Bitwarden(Login("p2")) with
        {
            Notes = "middle notes",
            RevisionDate = Middle,
            Origin = new ItemOrigin("bitwarden", BitwardenFile, "b2", null)
        };
        var newest = Bitwarden(Login("p3") with
        {
            Uris = [new LoginUri(ExampleUri), new LoginUri("https://other.example.com")]
        }) with
        {
            Origin = new ItemOrigin("bitwarden", "second.json", "b3", null)
        };

        var result = Run(keeper, middle, newest);

        var item = result.Merged.Items.ShouldHaveSingleItem();
        var login = item.Login.ShouldNotBeNull();
        login.Password.ShouldBe("p3");
        login.Totp.ShouldBe(KeeperTotp);
        login.Uris.Count.ShouldBe(2);
        item.Notes.ShouldBe("middle notes");
        item.PasswordHistory.ShouldBe([new PasswordHistoryEntry("p2", Middle), new PasswordHistoryEntry("p1", Older)]);
        item.Origin.SourceFile.ShouldBe("second.json");
        result.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_identical_items_produce_single_item_and_no_report_entries()
    {
        var keeper = Keeper(Login(NewPassword, KeeperTotp));
        var bitwarden = Bitwarden(Login(NewPassword, KeeperTotp));

        var result = Run(keeper, bitwarden);

        var item = result.Merged.Items.ShouldHaveSingleItem();
        item.Fields.ShouldBeEmpty();
        item.PasswordHistory.ShouldBeEmpty();
        result.Report.Groups.ShouldBeEmpty();
        result.Report.IdenticalGroups.ShouldBe(1);
        result.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_unmatched_items_pass_through_unchanged()
    {
        var keeper = Keeper(Login(OldPassword));
        var other = Bitwarden(Login(NewPassword) with
        {
            Uris = [new LoginUri("https://another.example.org")]
        }) with
        {
            Name = "Other"
        };

        var result = Run(keeper, other);

        result.Merged.Items.ShouldBe([keeper, other]);
        result.Report.UniqueGroups.ShouldBe(2);
        result.Report.Groups.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_type_change_keeps_other_type_data_as_hidden_fields()
    {
        var keeper = Keeper(Login(OldPassword, KeeperTotp));
        var bitwarden = new VaultItem
        {
            Type = VaultItemType.SecureNote,
            Name = "Example renamed",
            Notes = "newer notes",
            RevisionDate = Newer,
            Origin = new ItemOrigin("bitwarden", BitwardenFile, "b1", null),
        };

        var result = Run(new MergePolicy(), null, LinkKeeperAndBitwarden(), keeper, bitwarden);

        var item = result.Merged.Items.ShouldHaveSingleItem();
        item.Type.ShouldBe(VaultItemType.SecureNote);
        item.Name.ShouldBe("Example renamed");
        item.Login.ShouldBeNull();
        item.Fields.ShouldBe(
        [
            new CustomField("Name (from keeper.json)", "Example", CustomFieldType.Text),
            new CustomField("Username (from keeper.json)", Username, CustomFieldType.Hidden),
            new CustomField("Password (from keeper.json)", OldPassword, CustomFieldType.Hidden),
            new CustomField("TOTP (from keeper.json)", KeeperTotp, CustomFieldType.Hidden),
            new CustomField("URI (from keeper.json)", ExampleUri, CustomFieldType.Text),
        ]);
        item.ConversionNotes.ShouldContain(note => note.Contains("Type Login", StringComparison.Ordinal));
    }

    [Fact]
    public void Merge_keeps_two_files_with_one_name_and_size_whose_content_differs()
    {
        var keeper = Keeper(Login(OldPassword)) with
        {
            Attachments = [new AttachmentRef("scan.pdf", 100, "files/keeper-scan") { ContentHash = "aa01" }]
        };
        var bitwarden = Bitwarden(Login(OldPassword)) with
        {
            Attachments = [new AttachmentRef("scan.pdf", 100, "attachments/Example/scan.pdf") { ContentHash = "bb02" }]
        };

        var item = Run(keeper, bitwarden).Merged.Items.ShouldHaveSingleItem();

        item.Attachments.Select(attachment => attachment.SourcePath).ShouldBe(["attachments/Example/scan.pdf", "files/keeper-scan"]);
    }

    [Fact]
    public void Merge_keeps_every_file_whose_content_is_unknown()
    {
        var keeper = Keeper(Login(OldPassword)) with
        {
            Attachments = [new AttachmentRef("scan.pdf", 100, null)]
        };
        var bitwarden = Bitwarden(Login(OldPassword)) with
        {
            Attachments = [new AttachmentRef("scan.pdf", 100, null), new AttachmentRef("scan.pdf", 100, null)],
        };

        var item = Run(keeper, bitwarden).Merged.Items.ShouldHaveSingleItem();

        item.Attachments.Count.ShouldBe(3);
    }

    [Fact]
    public void Merge_keeps_one_copy_of_a_file_found_on_two_members_from_the_newest_member()
    {
        var keeper = Keeper(Login(OldPassword)) with
        {
            Attachments = [new AttachmentRef("scan.pdf", 100, "files/keeper-scan") { ContentHash = "aa01" }, new AttachmentRef("notes.txt", 7, "files/keeper-notes") { ContentHash = "cc03" }],
        };
        var bitwarden = Bitwarden(Login(OldPassword)) with
        {
            Attachments = [new AttachmentRef("scan.pdf", 100, "attachments/Example/scan.pdf") { ContentHash = "aa01" }]
        };

        var item = Run(keeper, bitwarden).Merged.Items.ShouldHaveSingleItem();

        item.Attachments.Select(attachment => attachment.SourcePath).ShouldBe(["attachments/Example/scan.pdf", "files/keeper-notes"]);
    }

    [Fact]
    public void Merge_never_drops_a_non_empty_value()
    {
        var random = new Random(20261022);

        for (int iteration = 0; iteration < 300; iteration++)
        {
            var (first, second) = MergeValueGenerator.Pair(random);
            var policy = MergeValueGenerator.Policy(random);

            var result = Run(policy, null, LinkKeeperAndBitwarden(), first, second);

            var merged = MergedValues.Collect(result.Merged.Items.ShouldHaveSingleItem());
            foreach (string value in MergedValues.Inputs(first).Concat(MergedValues.Inputs(second)))
            {
                merged.ShouldContain(candidate => candidate.Contains(value, StringComparison.Ordinal));
            }
        }
    }
}
