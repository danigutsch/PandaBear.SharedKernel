using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

public sealed class SecretPlacerTests
{
    [Fact]
    public void Place_rejected_finding_leaves_item_unchanged()
    {
        var item = SecretTestItems.Login(notes: "Password: Tr0ub4dor&3x\nPIN: 4821");
        var findings = SecretScanner.Default.Scan(item);

        var placed = SecretPlacer.Apply(item, []);

        findings.ShouldHaveCount(2);
        placed.Item.ShouldBeSameAs(item);
        placed.NewItems.ShouldBeEmpty();
        placed.Problems.ShouldBeEmpty();
    }

    [Fact]
    public void Place_accepted_finding_removes_source_text_from_notes_only_after_accept()
    {
        const string Notes = "Password: Tr0ub4dor&3x\nPIN: 4821\nkeep this line";
        var item = SecretTestItems.Login(notes: Notes);
        var findings = SecretScanner.Default.Scan(item);
        var pin = findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Pin);

        var placed = SecretPlacer.Apply(item, [pin]);

        item.Notes.ShouldBe(Notes);
        placed.Item.Notes.ShouldBe("Password: Tr0ub4dor&3x\nkeep this line");
        var field = placed.Item.Fields.ShouldHaveSingleItem();
        field.Name.ShouldBe("PIN");
        field.Value.ShouldBe("4821");
        var password = placed.Item.Login?.Password;
        password.ShouldBeNull();
    }

    [Fact]
    public void Place_value_over_field_budget_stays_in_notes_and_is_reported()
    {
        string huge = TestSecrets.FromAlphabet(TestSecrets.Base64, 4000, 40);
        string notes = "Password: " + huge;
        var item = SecretTestItems.Login(notes: notes);
        var finding = SecretScanner.Default.Scan(item).ShouldHaveSingleItem(found => found.Kind == SecretKind.Password);

        var placed = SecretPlacer.Apply(item, [finding]);

        placed.Item.Notes.ShouldBe(notes);
        var password = placed.Item.Login?.Password;
        password.ShouldBeNull();
        string problem = placed.Problems.ShouldHaveSingleItem();
        problem.ShouldContain(finding.Id, StringComparison.Ordinal);
        problem.ShouldNotContain(huge, StringComparison.Ordinal);
    }

    [Fact]
    public void Place_stale_location_is_reported_instead_of_cutting_wrong_text()
    {
        var item = SecretTestItems.Login(notes: "PIN: 4821");
        var finding = SecretScanner.Default.Scan(item).ShouldHaveSingleItem();
        var changed = item with { Notes = "PIN" };

        var placed = SecretPlacer.Apply(changed, [finding]);

        placed.Item.Notes.ShouldBe("PIN");
        placed.Item.Fields.ShouldBeEmpty();
        placed.Problems.ShouldHaveSingleItem();
    }

    [Fact]
    public void Place_card_number_from_card_note_creates_card_item()
    {
        string number = TestSecrets.CardNumber("4", 16);
        var item = SecretTestItems.Note("Travel card " + number, "Visa travel");
        var finding = SecretScanner.Default.Scan(item).ShouldHaveSingleItem();

        var placed = SecretPlacer.Apply(item, [finding]);

        var card = placed.NewItems.ShouldHaveSingleItem();
        card.Type.ShouldBe(VaultItemType.Card);
        var data = card.Card.ShouldNotBeNull();
        data.Number.ShouldBe(number);
        data.Brand.ShouldBe("Visa");
        placed.Item.Notes.ShouldBe("Travel card");
    }

    [Fact]
    public void Place_token_fills_empty_password_and_names_later_tokens_by_rule()
    {
        string first = TestSecrets.GitHubToken("ghp", 50);
        string second = TestSecrets.NpmToken(51);
        var item = SecretTestItems.Login(notes: first + "\n" + second);
        var findings = SecretScanner.Default.Scan(item);

        var placed = SecretPlacer.Apply(item, findings);

        findings.Select(finding => finding.ProposedTarget).ShouldBe([PlacementTarget.LoginPassword, PlacementTarget.Hidden("npm token")]);
        var password = placed.Item.Login?.Password;
        password.ShouldBe(first);
        placed.Item.Fields.ShouldHaveSingleItem().Value.ShouldBe(second);
        placed.Item.Notes.ShouldBeNull();
    }

    [Fact]
    public void Place_attachment_finding_reads_supplied_text_and_never_edits_notes()
    {
        string secret = TestSecrets.Base32Encode(TestSecrets.Bytes(60, 20));
        string text = "otpauth://totp/Example?secret=" + secret;
        string path = SecretLocation.Attachment("totp.txt");
        var item = SecretTestItems.Login(notes: "unrelated");
        var finding = SecretScanner.Default.ScanText(text, path, "totp.txt", item).ShouldHaveSingleItem();

        var placed = SecretPlacer.Apply(item, [finding], new Dictionary<string, string> { [path] = text });

        string totp = placed.Item.Login.ShouldNotBeNull().Totp.ShouldNotBeNull();
        totp.ShouldContain(secret, StringComparison.Ordinal);
        placed.Item.Notes.ShouldBe("unrelated");
    }
}
