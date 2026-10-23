using System.Security.Cryptography;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Review;
using PandaBear.VaultManager.Tool.Secrets;
using PandaBear.VaultManager.Tool.Tests.Pipeline;
using PandaBear.VaultManager.Tool.Tests.Secrets;
using static PandaBear.VaultManager.Tool.Tests.Review.ReviewVaults;

namespace PandaBear.VaultManager.Tool.Tests.Review;

public sealed class ReviewSessionTests : IDisposable
{
    private const string NotesPassword = "Gh-notes-Passw0rd!";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-review-session-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void Notes_decisions_are_set_replaced_and_removed_per_item()
    {
        VaultItem wifi = KeeperNote("rec-3", "Wifi", "fixture wifi note");
        var session = Session(Scan(directory, wifi));

        session.DecideNotes(OriginKey(wifi), NotesDecision.Share);
        session.Decisions.Notes.ShouldHaveSingleItem().NotesSha256.ShouldBe(ItemNotesDecision.HashOf("fixture wifi note"));
        session.DecideNotes(OriginKey(wifi), NotesDecision.Hidden);

        session.Decisions.Notes.ShouldBe([new ItemNotesDecision(OriginKey(wifi), NotesDecision.Hidden, "Wifi", 0)]);
        session.IsDirty.ShouldBeTrue();
        session.DecideNotes(OriginKey(wifi), NotesDecision.Pending);
        session.Decisions.Notes.ShouldBeEmpty();
        var missing = () => session.DecideNotes("keeper|keeper.json|missing", NotesDecision.Share);
        missing.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Sharing_notes_without_findings_skips_notes_that_hold_a_finding_or_a_decision()
    {
        VaultItem wifi = KeeperNote("rec-3", "Wifi", "fixture wifi note");
        VaultItem router = KeeperNote("rec-4", "Router", "fixture router note");
        VaultItem github = CodesAndPasswordInNotes("rec-github", PipelineSecrets.GitHubRecoveryCodes(720), NotesPassword);
        var session = Session(Scan(directory, wifi, router, github), MergeDecisions.None with { Notes = [new ItemNotesDecision(OriginKey(router), NotesDecision.Private)] });

        session.ShareNotesWithoutFindings().ShouldBe(1);

        session.Decisions.FindNotes(OriginKey(wifi)).ShouldBe(NotesDecision.Share);
        session.Decisions.Notes.ShouldContain(entry => entry.OriginKey == OriginKey(wifi) && entry.NotesSha256 == ItemNotesDecision.HashOf("fixture wifi note"));
        session.Decisions.FindNotes(OriginKey(router)).ShouldBe(NotesDecision.Private);
        session.Decisions.FindNotes(OriginKey(github)).ShouldBe(NotesDecision.Pending);
    }

    [Fact]
    public void Groups_filter_by_pending_conflicting_identical_and_unique_status()
    {
        VaultItem keeperExample = KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com");
        VaultItem bitwardenExample = BitwardenLogin("bw-1", "Example", "alice", BitwardenPassword, "https://example.com");
        VaultItem keeperMail = KeeperLogin("rec-2", "Mail", "bob", KeeperPassword, "https://mail.example.com", PipelineInputs.KeeperModified);
        VaultItem bitwardenMail = BitwardenLogin("bw-2", "Mail", "bob", KeeperPassword, "https://mail.example.com", PipelineInputs.BitwardenRevision);
        VaultItem wifi = KeeperNote("rec-3", "Wifi", "fixture wifi note");
        var session = Session(Scan(directory, keeperExample, bitwardenExample, keeperMail, bitwardenMail, wifi));

        var all = session.Groups(GroupFilter.All, null);

        all.Count.ShouldBe(3);
        var example = session.Groups(GroupFilter.Pending, null).ShouldHaveSingleItem();
        example.MemberKeys.ShouldBe([OriginKey(keeperExample), OriginKey(bitwardenExample)]);
        example.PendingFields.ShouldBe(1);
        example.ConflictingFields.ShouldBe([FieldPaths.LoginPassword]);
        session.Groups(GroupFilter.Conflict, null).ShouldHaveSingleItem().Key.ShouldBe(example.Key);
        session.Groups(GroupFilter.Identical, null).ShouldHaveSingleItem().MemberKeys.ShouldBe([OriginKey(keeperMail), OriginKey(bitwardenMail)]);
        session.Groups(GroupFilter.Unique, null).ShouldHaveSingleItem().MemberKeys.ShouldBe([OriginKey(wifi)]);
    }

    [Fact]
    public void Groups_filter_by_findings_and_by_suggested_matches()
    {
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(500);
        VaultItem keeper = CodesAndPasswordInNotes("rec-github", codes, NotesPassword);
        VaultItem work = BitwardenLogin("bw-work", "GitHub work", "bob", BitwardenPassword, "https://github.com");
        VaultItem wifi = KeeperNote("rec-wifi", "Wifi", "fixture wifi note");
        var session = Session(Scan(directory, keeper, work, wifi));

        var withFindings = session.Groups(GroupFilter.HasFindings, null);
        var suggested = session.Groups(GroupFilter.Suggested, null);

        var github = withFindings.ShouldHaveSingleItem();
        github.MemberKeys.ShouldBe([OriginKey(keeper)]);
        github.Findings.ShouldBe(2);
        github.PendingFindings.ShouldBe(1);
        github.IsPending.ShouldBeTrue();
        suggested.Select(group => group.MemberKeys[0]).ShouldBe([OriginKey(keeper), OriginKey(work)]);
        session.Groups(GroupFilter.Unique, null).Count.ShouldBe(3);
    }

    [Theory]
    [InlineData("exam")]
    [InlineData("EXAMPLE")]
    [InlineData(" example ")]
    public void Search_finds_groups_by_member_name_ignoring_case(string search)
    {
        var session = Session(Scan(
            directory,
            KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com"),
            KeeperNote("rec-2", "Wifi", "fixture wifi note")));

        var found = session.Groups(GroupFilter.All, search);

        found.ShouldHaveSingleItem().Name.ShouldBe("Example");
    }

    [Fact]
    public void Fields_show_each_member_value_and_mask_the_password_by_default()
    {
        var session = Session(ConflictingLogins(directory));
        var group = GroupOf(session, KeeperKey);

        var username = FieldOf(session, group.Key, FieldPaths.LoginUsername);
        var password = FieldOf(session, group.Key, FieldPaths.LoginPassword);

        username.Values.ShouldBe(["alice", "alice"]);
        username.IsSecret.ShouldBeFalse();
        password.Values.ShouldBe([ReviewText.Mask, ReviewText.Mask]);
        password.IsSecret.ShouldBeTrue();
        password.IsRevealed.ShouldBeFalse();
        password.Differs.ShouldBeTrue();
        password.IsConflict.ShouldBeTrue();
        password.Resolution.ShouldBe(ResolutionKind.Unresolved);
        password.Choice.ShouldBeNull();
        password.AcceptsMember.ShouldBeTrue();
        password.AcceptsEmpty.ShouldBeTrue();
        session.Members(group.Key).Select(member => member.SourceFile).ShouldBe([KeeperFile, BitwardenFile]);
    }

    [Fact]
    public void Revealing_a_field_shows_its_values_until_it_is_masked_again()
    {
        var session = Session(ConflictingLogins(directory));
        string groupKey = GroupOf(session, KeeperKey).Key;

        bool revealed = session.ToggleReveal(groupKey, FieldPaths.LoginPassword);
        var shown = FieldOf(session, groupKey, FieldPaths.LoginPassword);
        bool revealedAgain = session.ToggleReveal(groupKey, FieldPaths.LoginPassword);
        var hidden = FieldOf(session, groupKey, FieldPaths.LoginPassword);

        revealed.ShouldBeTrue();
        shown.IsRevealed.ShouldBeTrue();
        shown.Values.ShouldBe([KeeperPassword, BitwardenPassword]);
        revealedAgain.ShouldBeFalse();
        hidden.Values.ShouldBe([ReviewText.Mask, ReviewText.Mask]);
        session.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void Secret_fields_are_masked_by_default_and_no_secret_shows_before_it_is_revealed()
    {
        string totp = PipelineSecrets.TotpUri(PipelineSecrets.TotpSecret(510));
        string[] cardNumbers = [TestSecrets.CardNumber("4", 16), TestSecrets.CardNumber("51", 16)];
        string[] cardCodes = [TestSecrets.FromAlphabet("0123456789", 3, 511), TestSecrets.FromAlphabet("0123456789", 3, 512)];
        using var firstKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var secondKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string[] privateKeys = [PemFixtures.Pkcs8(firstKey), PemFixtures.Pkcs8(secondKey)];
        string[] hidden = ["fixture-hidden-keeper", "fixture-hidden-bitwarden"];
        string[] history = ["fixture-old-keeper-password", "fixture-old-bitwarden-password"];
        VaultItem[] items =
        [
            .. new[] { (Provider: "keeper", File: KeeperFile), (Provider: "bitwarden", File: BitwardenFile) }.SelectMany((source, index) => new VaultItem[]
            {
                Login(source.Provider, source.File, "login", "Bank", "carol", index == 0 ? KeeperPassword : BitwardenPassword, "https://bank.example.com", null) with
                {
                    Login = new LoginData
                    {
                        Username = "carol",
                        Password = index == 0 ? KeeperPassword : BitwardenPassword,
                        Totp = index == 0 ? totp : null,
                        Uris = [new LoginUri("https://bank.example.com")],
                    },
                    Fields = [new CustomField("PIN field", hidden[index], CustomFieldType.Hidden)],
                    PasswordHistory = [new PasswordHistoryEntry(history[index], PipelineInputs.KeeperModified)],
                },
                new()
                {
                    Type = VaultItemType.Card,
                    Name = "Visa",
                    Card = new CardData { CardholderName = "Carol", Number = cardNumbers[index], Code = cardCodes[index] },
                    Origin = new ItemOrigin(source.Provider, source.File, "card", null),
                },
                new()
                {
                    Type = VaultItemType.SshKey,
                    Name = "Deploy key",
                    SshKey = new SshKeyData { PrivateKey = privateKeys[index] },
                    Origin = new ItemOrigin(source.Provider, source.File, "ssh", null),
                },
            }),
        ];
        var session = Session(Scan(directory, items));
        string[] secrets = [KeeperPassword, BitwardenPassword, totp, .. cardNumbers, .. cardCodes, .. privateKeys, .. hidden, .. history];

        ReviewField[] fields = [.. session.Groups(GroupFilter.All, null).SelectMany(group => session.Fields(group.Key))];

        string[] secretPaths = [.. fields.Where(field => field.IsSecret).Select(field => field.FieldPath)];
        secretPaths.ShouldBe([FieldPaths.LoginPassword, FieldPaths.LoginTotp, FieldPaths.Fields, FieldPaths.PasswordHistory, "card.number", "card.code", "sshKey.privateKey"]);
        string shown = string.Join("\n", fields.SelectMany(field => field.Values));
        secrets.ShouldAllSatisfy(secret => shown.ShouldNotContain(secret, StringComparison.Ordinal));
        shown.ShouldContain("Carol", StringComparison.Ordinal);
        shown.ShouldContain("PIN field", StringComparison.Ordinal);
    }

    [Fact]
    public void Identity_numbers_and_the_credentials_and_queries_of_websites_are_masked_by_default()
    {
        VaultItem identity = new()
        {
            Type = VaultItemType.Identity,
            Name = "Identity",
            Identity = new IdentityData { FirstName = "Carol", Ssn = "fixture-ssn", PassportNumber = "fixture-passport", LicenseNumber = "fixture-license" },
            Origin = new ItemOrigin("keeper", KeeperFile, "identity", null),
        };
        VaultItem login = KeeperLogin("site", "Example", "carol", null, "https://u:fixture-uri-password@example.com/Path?token=fixture-uri-token#fixture-fragment");
        var session = Session(Scan(directory, identity, login));

        ReviewField[] fields = [.. session.Groups(GroupFilter.All, null).SelectMany(group => session.Fields(group.Key))];

        string[] secretPaths = [.. fields.Where(field => field.IsSecret).Select(field => field.FieldPath).Order(StringComparer.Ordinal)];
        secretPaths.ShouldBe(["identity.licenseNumber", "identity.passportNumber", "identity.ssn", FieldPaths.LoginUris]);
        string shown = string.Join("\n", fields.SelectMany(field => field.Values));
        string[] secrets = ["fixture-ssn", "fixture-passport", "fixture-license", "fixture-uri-password", "fixture-uri-token", "fixture-fragment"];
        secrets.ShouldAllSatisfy(secret => shown.ShouldNotContain(secret, StringComparison.Ordinal));
        shown.ShouldContain("https://" + ReviewText.Mask + "@example.com/Path?" + ReviewText.Mask, StringComparison.Ordinal);
        shown.ShouldContain("Carol", StringComparison.Ordinal);
    }

    [Fact]
    public void Choosing_a_member_decides_the_field_and_marks_the_review_dirty()
    {
        var session = Session(ConflictingLogins(directory));
        string groupKey = GroupOf(session, KeeperKey).Key;

        session.ChooseMember(groupKey, FieldPaths.LoginPassword, KeeperKey);

        session.IsDirty.ShouldBeTrue();
        session.Pending.IsEmpty.ShouldBeTrue();
        session.Decisions.Find(groupKey, FieldPaths.LoginPassword).ShouldBe(FieldChoice.Member(FieldPaths.LoginPassword, KeeperKey));
        session.Result.Merged.Items.ShouldHaveSingleItem().Login.ShouldNotBeNull().Password.ShouldBe(KeeperPassword);
        var password = FieldOf(session, groupKey, FieldPaths.LoginPassword);
        password.Choice.ShouldBe(FieldChoice.Member(FieldPaths.LoginPassword, KeeperKey));
        password.Resolution.ShouldBe(ResolutionKind.Decision);
        session.Groups(GroupFilter.Pending, null).ShouldBeEmpty();
    }

    [Fact]
    public void Choosing_a_member_for_every_conflict_decides_each_conflicting_field()
    {
        VaultItem keeper = KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com") with { Notes = "fixture keeper notes" };
        VaultItem bitwarden = BitwardenLogin("bw-1", "Example account", "alice", BitwardenPassword, "https://example.com") with { Notes = "fixture bitwarden notes" };
        var session = Session(Scan(directory, keeper, bitwarden));
        string groupKey = GroupOf(session, OriginKey(keeper)).Key;

        int decided = session.ChooseMemberForConflicts(groupKey, OriginKey(bitwarden));

        decided.ShouldBe(3);
        string[] decidedPaths = [.. session.Decisions.Groups.ShouldHaveSingleItem().Choices.Select(choice => choice.FieldPath)];
        decidedPaths.ShouldBe([FieldPaths.Name, FieldPaths.Notes, FieldPaths.LoginPassword]);
        session.Pending.IsEmpty.ShouldBeTrue();
        var merged = session.Result.Merged.Items.ShouldHaveSingleItem();
        merged.Name.ShouldBe("Example account");
        merged.Notes.ShouldBe("fixture bitwarden notes");
        merged.Login.ShouldNotBeNull().Password.ShouldBe(BitwardenPassword);
    }

    [Fact]
    public void Choosing_empty_drops_a_value_that_only_one_member_holds()
    {
        string totp = PipelineSecrets.TotpUri(PipelineSecrets.TotpSecret(520));
        VaultItem keeper = KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com", PipelineInputs.KeeperModified);
        keeper = keeper with { Login = keeper.Login! with { Totp = totp } };
        VaultItem bitwarden = BitwardenLogin("bw-1", "Example", "alice", KeeperPassword, "https://example.com", PipelineInputs.BitwardenRevision);
        var session = Session(Scan(directory, keeper, bitwarden));
        string groupKey = GroupOf(session, OriginKey(keeper)).Key;

        session.ChooseEmpty(groupKey, FieldPaths.LoginTotp);

        session.Decisions.Find(groupKey, FieldPaths.LoginTotp).ShouldBe(FieldChoice.Empty(FieldPaths.LoginTotp));
        session.Result.Merged.Items.ShouldHaveSingleItem().Login.ShouldNotBeNull().Totp.ShouldBeNull();
        FieldOf(session, groupKey, FieldPaths.LoginTotp).Resolution.ShouldBe(ResolutionKind.Decision);
    }

    [Fact]
    public void Clearing_a_choice_leaves_the_field_to_the_policy_again()
    {
        var session = Session(ConflictingLogins(directory));
        string groupKey = GroupOf(session, KeeperKey).Key;
        session.ChooseMember(groupKey, FieldPaths.LoginPassword, BitwardenKey);

        session.ClearChoice(groupKey, FieldPaths.LoginPassword);

        session.Decisions.Find(groupKey, FieldPaths.LoginPassword).ShouldBeNull();
        session.Pending.Fields.ShouldHaveSingleItem().FieldPath.ShouldBe(FieldPaths.LoginPassword);
        session.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void Choosing_an_item_outside_the_group_or_a_member_for_a_list_is_refused()
    {
        var session = Session(ConflictingLogins(directory));
        string groupKey = GroupOf(session, KeeperKey).Key;

        var outsider = () => session.ChooseMember(groupKey, FieldPaths.LoginPassword, "keeper|keeper.json|rec-unknown");
        var list = () => session.ChooseMember(groupKey, FieldPaths.LoginUris, KeeperKey);

        outsider.ShouldThrow<ArgumentException>();
        list.ShouldThrow<ArgumentException>();
        session.Decisions.Groups.ShouldBeEmpty();
        session.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void Accepting_a_proposed_finding_moves_its_secret_and_leaves_nothing_pending()
    {
        VaultItem keeper = CodesAndPasswordInNotes("rec-github", PipelineSecrets.GitHubRecoveryCodes(530), NotesPassword);
        var session = Session(Scan(directory, keeper));
        string groupKey = GroupOf(session, OriginKey(keeper)).Key;
        var proposed = session.Findings(groupKey).ShouldHaveSingleItem(finding => finding.IsPending);

        session.DecideFinding(OriginKey(keeper), proposed.FindingId, FindingDecision.Accepted);

        session.Pending.IsEmpty.ShouldBeTrue();
        var accepted = session.Findings(groupKey).ShouldHaveSingleItem(finding => finding.FindingId == proposed.FindingId);
        accepted.Decision.ShouldBe(FindingDecision.Accepted);
        accepted.IsApplied.ShouldBeTrue();
        session.Result.Merged.Items.ShouldHaveSingleItem().Login.ShouldNotBeNull().Password.ShouldBe(NotesPassword);
        session.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void Rejecting_a_pre_accepted_finding_leaves_its_secret_in_place_until_the_decision_is_cleared()
    {
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(540);
        VaultItem keeper = CodesAndPasswordInNotes("rec-github", codes, NotesPassword);
        var session = Session(Scan(directory, keeper));
        string groupKey = GroupOf(session, OriginKey(keeper)).Key;
        var preAccepted = session.Findings(groupKey).ShouldHaveSingleItem(finding => finding.Kind == SecretKind.RecoveryCodes);

        session.DecideFinding(OriginKey(keeper), preAccepted.FindingId, FindingDecision.Rejected);
        string rejectedNotes = session.Result.Merged.Items.ShouldHaveSingleItem().Notes.ShouldNotBeNull();
        bool rejectedApplied = session.Findings(groupKey).ShouldHaveSingleItem(finding => finding.FindingId == preAccepted.FindingId).IsApplied;
        session.ClearFinding(OriginKey(keeper), preAccepted.FindingId);

        preAccepted.IsApplied.ShouldBeTrue();
        rejectedApplied.ShouldBeFalse();
        rejectedNotes.ShouldContain(codes[0], StringComparison.Ordinal);
        session.Decisions.FindFinding(OriginKey(keeper), preAccepted.FindingId).ShouldBeNull();
        session.Result.Merged.Items.ShouldHaveSingleItem().Notes.ShouldNotBeNull().ShouldNotContain(codes[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Finding_values_are_masked_until_the_finding_is_revealed()
    {
        VaultItem keeper = CodesAndPasswordInNotes("rec-github", PipelineSecrets.GitHubRecoveryCodes(550), NotesPassword);
        var session = Session(Scan(directory, keeper));
        string groupKey = GroupOf(session, OriginKey(keeper)).Key;
        var masked = session.Findings(groupKey).ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Password);

        bool revealed = session.ToggleRevealFinding(OriginKey(keeper), masked.FindingId);
        var shown = session.Findings(groupKey).ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Password);

        masked.Value.ShouldBe(ReviewText.Mask);
        revealed.ShouldBeTrue();
        shown.Value.ShouldContain(NotesPassword, StringComparison.Ordinal);
        session.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void Notes_that_hold_a_finding_are_masked_by_default()
    {
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(555);
        VaultItem keeper = CodesAndPasswordInNotes("rec-github", codes, NotesPassword);
        var session = Session(Scan(directory, keeper));
        string groupKey = GroupOf(session, OriginKey(keeper)).Key;

        var notes = FieldOf(session, groupKey, FieldPaths.Notes);

        notes.IsSecret.ShouldBeTrue();
        notes.Values.ShouldBe([ReviewText.Mask]);
    }

    [Fact]
    public void User_names_that_hold_a_finding_are_masked_also_after_the_finding_is_rejected()
    {
        string seed = PipelineSecrets.TotpSecret(556);
        string uri = PipelineSecrets.TotpUri(PipelineSecrets.TotpSecret(557));
        VaultItem seeded = KeeperLogin("rec-seed", "Seeded", seed, KeeperPassword, "https://seeded.example.com");
        VaultItem authenticator = KeeperLogin("rec-uri", "Authenticator", uri, KeeperPassword, "https://otp.example.com");
        var session = Session(Scan(directory, seeded, authenticator));
        string seededGroup = GroupOf(session, OriginKey(seeded)).Key;
        string uriGroup = GroupOf(session, OriginKey(authenticator)).Key;
        var uriFinding = session.Findings(uriGroup).ShouldHaveSingleItem();

        session.DecideFinding(OriginKey(authenticator), uriFinding.FindingId, FindingDecision.Rejected);
        var seededUsername = FieldOf(session, seededGroup, FieldPaths.LoginUsername);
        var rejectedUsername = FieldOf(session, uriGroup, FieldPaths.LoginUsername);
        string shown = string.Join("\n", session.Groups(GroupFilter.All, null).SelectMany(group => session.Fields(group.Key)).SelectMany(field => field.Values));
        bool revealed = session.ToggleReveal(seededGroup, FieldPaths.LoginUsername);

        uriFinding.IsPreAccepted.ShouldBeTrue();
        seededUsername.IsSecret.ShouldBeTrue();
        seededUsername.Values.ShouldBe([ReviewText.Mask]);
        rejectedUsername.IsSecret.ShouldBeTrue();
        rejectedUsername.Values.ShouldBe([ReviewText.Mask]);
        shown.ShouldNotContain(seed, StringComparison.Ordinal);
        shown.ShouldNotContain(uri, StringComparison.Ordinal);
        revealed.ShouldBeTrue();
        FieldOf(session, seededGroup, FieldPaths.LoginUsername).Values.ShouldBe([seed]);
    }

    [Fact]
    public void An_accepted_finding_that_placement_leaves_in_place_shows_why_instead_of_applied()
    {
        string huge = TestSecrets.FromAlphabet(TestSecrets.Base64, 4000, 558);
        VaultItem keeper = KeeperLogin("rec-long", "Long", "alice", null, "https://long.example.com", PipelineInputs.KeeperModified) with { Notes = "Password: " + huge };
        var session = Session(Scan(directory, keeper));
        string groupKey = GroupOf(session, OriginKey(keeper)).Key;
        var finding = session.Findings(groupKey).ShouldHaveSingleItem(found => found.Kind == SecretKind.Password);

        session.DecideFinding(OriginKey(keeper), finding.FindingId, FindingDecision.Accepted);

        var accepted = session.Findings(groupKey).ShouldHaveSingleItem(found => found.FindingId == finding.FindingId);
        accepted.Decision.ShouldBe(FindingDecision.Accepted);
        accepted.IsApplied.ShouldBeFalse();
        accepted.Problem.ShouldBe("value exceeds the Bitwarden field budget; left in place");
        session.Result.Merged.Items.ShouldHaveSingleItem().Notes.ShouldNotBeNull().ShouldContain(huge, StringComparison.Ordinal);
    }

    [Fact]
    public void Masked_custom_fields_show_their_names_on_one_line()
    {
        VaultItem keeper = KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com") with
        {
            Fields = [new CustomField("\u001b]0;TITLE\u0007PIN", "fixture-hidden-pin", CustomFieldType.Hidden)],
        };
        var session = Session(Scan(directory, keeper));

        var fields = FieldOf(session, GroupOf(session, OriginKey(keeper)).Key, FieldPaths.Fields);

        fields.IsSecret.ShouldBeTrue();
        fields.Values.ShouldBe(["�]0;TITLE�PIN: " + ReviewText.Mask]);
    }

    [Fact]
    public void Linking_two_items_puts_them_into_one_group()
    {
        VaultItem keeper = KeeperLogin("rec-1", "GitHub", "alice", KeeperPassword, "https://github.com", PipelineInputs.KeeperModified);
        VaultItem bitwarden = BitwardenLogin("bw-1", "GitHub work", "alice.work", KeeperPassword, "https://github.com", PipelineInputs.BitwardenRevision);
        var session = Session(Scan(directory, keeper, bitwarden));

        bool linked = session.Link(OriginKey(keeper), OriginKey(bitwarden));

        linked.ShouldBeTrue();
        GroupOf(session, OriginKey(keeper)).MemberKeys.ShouldBe([OriginKey(keeper), OriginKey(bitwarden)]);
        session.Decisions.Overrides.Links.ShouldBe([new OriginKeyPair(OriginKey(keeper), OriginKey(bitwarden))]);
        session.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void Linking_items_that_a_split_keeps_apart_removes_the_split()
    {
        var split = new MergeDecisions([], new MatchOverrides([], [new OriginKeyPair(BitwardenKey, KeeperKey)]));
        var session = Session(ConflictingLogins(directory), split);
        int separate = session.Groups(GroupFilter.All, null).Count;

        bool linked = session.Link(KeeperKey, BitwardenKey);

        separate.ShouldBe(2);
        linked.ShouldBeTrue();
        session.Decisions.Overrides.Splits.ShouldBeEmpty();
        session.Groups(GroupFilter.All, null).ShouldHaveSingleItem().MemberKeys.ShouldBe([KeeperKey, BitwardenKey]);
    }

    [Fact]
    public void Splitting_an_item_moves_it_into_a_group_of_its_own()
    {
        var session = Session(ConflictingLogins(directory));

        session.Split(BitwardenKey);

        session.Groups(GroupFilter.All, null).Select(group => group.MemberKeys.ShouldHaveSingleItem()).ShouldBe([KeeperKey, BitwardenKey]);
        session.Decisions.Overrides.Splits.ShouldBe([new OriginKeyPair(BitwardenKey, KeeperKey)]);
        session.Pending.IsEmpty.ShouldBeTrue();
        session.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void Pending_entries_of_the_decisions_count_as_no_decision()
    {
        var conflicts = Session(ConflictingLogins(directory));
        var pending = conflicts.Decisions.WithPending(conflicts.Pending.Fields, conflicts.Pending.Findings);

        var session = Session(ConflictingLogins(directory), pending);

        pending.Groups.ShouldHaveSingleItem().Choices.ShouldHaveSingleItem().Kind.ShouldBe(FieldChoiceKind.Pending);
        session.Decisions.Groups.ShouldBeEmpty();
        session.Pending.Fields.ShouldHaveSingleItem().FieldPath.ShouldBe(FieldPaths.LoginPassword);
        session.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void Revealed_fields_and_findings_print_no_values()
    {
        VaultItem keeper = CodesAndPasswordInNotes("rec-github", PipelineSecrets.GitHubRecoveryCodes(560), NotesPassword);
        var session = Session(Scan(directory, keeper));
        string groupKey = GroupOf(session, OriginKey(keeper)).Key;
        session.ToggleReveal(groupKey, FieldPaths.Notes);
        var finding = session.Findings(groupKey).ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Password);
        session.ToggleRevealFinding(OriginKey(keeper), finding.FindingId);

        string printed = string.Join("\n", session.Fields(groupKey).Select(field => field.ToString()).Concat(session.Findings(groupKey).Select(finding => finding.ToString())));

        FieldOf(session, groupKey, FieldPaths.Notes).Values.ShouldHaveSingleItem().ShouldContain(NotesPassword, StringComparison.Ordinal);
        printed.ShouldNotContain(NotesPassword, StringComparison.Ordinal);
        printed.ShouldContain(FieldPaths.Notes, StringComparison.Ordinal);
    }
}
