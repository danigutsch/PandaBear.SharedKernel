using System.Security.Cryptography;
using System.Text.RegularExpressions;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

public sealed class SecretScannerTests
{
    private static readonly SecretScanner Scanner = SecretScanner.Default;

    [Fact]
    public void Scan_github_recovery_codes_block_in_notes_proposes_hidden_recovery_codes_field()
    {
        string codes = string.Join('\n', Enumerable.Range(0, 16).Select(index =>
            TestSecrets.FromAlphabet(TestSecrets.LowerHex, 5, index) + "-" + TestSecrets.FromAlphabet(TestSecrets.LowerHex, 5, 100 + index)));
        var item = SecretTestItems.Login(notes: "GitHub recovery codes\n" + codes + "\n", uri: "https://github.com/login");

        var findings = Scanner.Scan(item);

        var finding = findings.ShouldHaveSingleItem();
        finding.Kind.ShouldBe(SecretKind.RecoveryCodes);
        finding.Confidence.ShouldBe(Confidence.High);
        finding.ProposedTarget.ShouldBe(PlacementTarget.Hidden("Recovery codes"));
        item.Notes.ShouldNotBeNull().Substring(finding.Location.Start, finding.Location.Length).ShouldBe(codes);
    }

    [Fact]
    public void Scan_eight_digit_runs_without_keyword_or_block_are_not_recovery_codes()
    {
        string block = string.Join('\n', Enumerable.Range(0, 8).Select(index => TestSecrets.FromAlphabet("0123456789", 8, index)));
        var item = SecretTestItems.Note("Order 48151623 shipped on Monday.\nInvoice 42424242 is paid.\n\n" + block);

        var findings = Scanner.Scan(item);

        findings.ShouldNotContain(finding => finding.Kind == SecretKind.RecoveryCodes);
    }

    [Fact]
    public void Scan_otpauth_uri_in_notes_proposes_login_totp_with_parameters_preserved()
    {
        string secret = TestSecrets.Base32Encode(TestSecrets.Bytes(1, 20));
        string uri = $"otpauth://totp/Example:alice@example.com?secret={secret}&issuer=Example&algorithm=SHA256&digits=8&period=60";
        var item = SecretTestItems.Login(notes: "Authenticator:\n" + uri);

        var finding = Scanner.Scan(item).ShouldHaveSingleItem();
        var placed = SecretPlacer.Apply(item, [finding]);

        finding.Kind.ShouldBe(SecretKind.Totp);
        finding.Confidence.ShouldBe(Confidence.High);
        finding.ProposedTarget.ShouldBe(PlacementTarget.LoginTotp);
        string totp = placed.Item.Login.ShouldNotBeNull().Totp.ShouldNotBeNull();
        totp.ShouldStartWith("otpauth://totp/Example:alice%40example.com?secret=" + secret, StringComparison.Ordinal);
        totp.ShouldContain("algorithm=SHA256", StringComparison.Ordinal);
        totp.ShouldContain("digits=8", StringComparison.Ordinal);
        totp.ShouldContain("period=60", StringComparison.Ordinal);
        placed.Problems.ShouldBeEmpty();
    }

    [Fact]
    public void Scan_base32_seed_under_2fa_key_proposes_login_totp()
    {
        string secret = TestSecrets.Base32Encode(TestSecrets.Bytes(2, 20));
        var item = SecretTestItems.Login(notes: "2FA: " + secret);

        var finding = Scanner.Scan(item).ShouldHaveSingleItem();

        finding.Kind.ShouldBe(SecretKind.Totp);
        finding.ProposedTarget.ShouldBe(PlacementTarget.LoginTotp);
    }

    [Fact]
    public void Scan_second_totp_goes_to_hidden_totp_field_when_login_totp_exists()
    {
        string existing = TestSecrets.Base32Encode(TestSecrets.Bytes(3, 20));
        string second = TestSecrets.Base32Encode(TestSecrets.Bytes(4, 20));
        var item = SecretTestItems.Login(totp: existing, notes: "otpauth://totp/Other?secret=" + second);

        var finding = Scanner.Scan(item).ShouldHaveSingleItem();

        finding.ProposedTarget.ShouldBe(PlacementTarget.Hidden("TOTP (2)"));
    }

    [Fact]
    public void Scan_totp_seed_in_password_field_is_reported_as_misfiled()
    {
        string secret = TestSecrets.Base32Encode(TestSecrets.Bytes(5, 20));
        var item = SecretTestItems.Login(password: secret);

        var finding = Scanner.Scan(item).ShouldHaveSingleItem();
        var placed = SecretPlacer.Apply(item, [finding]);

        finding.Kind.ShouldBe(SecretKind.Totp);
        finding.Location.FieldPath.ShouldBe(SecretLocation.LoginPassword);
        finding.ProposedTarget.ShouldBe(PlacementTarget.LoginTotp);
        var login = placed.Item.Login.ShouldNotBeNull();
        login.Password.ShouldBeNull();
        login.Totp.ShouldNotBeNull().ShouldContain(secret, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_password_line_in_notes_fills_empty_login_password()
    {
        var item = SecretTestItems.Login(notes: "Shared account\nPassword: Tr0ub4dor&3x\nAsk Bob first.");

        var finding = Scanner.Scan(item).ShouldHaveSingleItem();
        var placed = SecretPlacer.Apply(item, [finding]);

        finding.Kind.ShouldBe(SecretKind.Password);
        finding.ProposedTarget.ShouldBe(PlacementTarget.LoginPassword);
        var password = placed.Item.Login?.Password;
        password.ShouldBe("Tr0ub4dor&3x");
        placed.Item.Notes.ShouldBe("Shared account\nAsk Bob first.");
    }

    [Fact]
    public void Scan_password_line_in_notes_becomes_hidden_field_when_password_exists()
    {
        var item = SecretTestItems.Login(password: "current-password", notes: "Passwort: altes-Kennwort-42");

        var finding = Scanner.Scan(item).ShouldHaveSingleItem();
        var placed = SecretPlacer.Apply(item, [finding]);

        finding.ProposedTarget.ShouldBe(PlacementTarget.Hidden("Password (notes)"));
        var field = placed.Item.Fields.ShouldHaveSingleItem();
        field.Name.ShouldBe("Password (notes)");
        field.Value.ShouldBe("altes-Kennwort-42");
        field.Type.ShouldBe(CustomFieldType.Hidden);
        placed.Item.Notes.ShouldBeNull();
    }

    [Fact]
    public void Scan_security_question_lines_become_question_named_hidden_fields()
    {
        var item = SecretTestItems.Login(notes: "Q: What was your first pet?\nA: Rex the dog\nQuestion: City of birth?\nAnswer: Springfield");

        var findings = Scanner.Scan(item);
        var placed = SecretPlacer.Apply(item, findings);

        findings.ShouldHaveCount(2);
        findings.ShouldAllSatisfy(finding => finding.Kind.ShouldBe(SecretKind.SecurityQuestion));
        placed.Item.Fields.Select(field => (field.Name, field.Value, field.Type)).ShouldBe(
        [
            ("What was your first pet?", "Rex the dog", CustomFieldType.Hidden),
            ("City of birth?", "Springfield", CustomFieldType.Hidden),
        ]);
        placed.Item.Notes.ShouldBeNull();
    }

    [Fact]
    public void Scan_luhn_valid_card_number_is_detected_and_luhn_invalid_is_not()
    {
        string valid = TestSecrets.CardNumber("4", 16);
        char last = valid[^1] == '9' ? '0' : (char)(valid[^1] + 1);
        string invalid = valid[..^1] + last;
        string notes = $"Card: {valid[..4]} {valid[4..8]} {valid[8..12]} {valid[12..]}\nOld card: {invalid}";
        var item = SecretTestItems.Note(notes);

        var finding = Scanner.Scan(item).ShouldHaveSingleItem(found => found.Kind == SecretKind.CardNumber);

        finding.Confidence.ShouldBe(Confidence.High);
        finding.Location.Start.ShouldBe(6);
        finding.ProposedTarget.ShouldBe(PlacementTarget.NewCardItem);
        Scanner.Scan(item).ShouldHaveCount(1);
    }

    [Fact]
    public void Scan_iban_with_bad_checksum_is_not_detected()
    {
        string valid = TestSecrets.Iban("DE", "370400440532013000");
        string broken = valid[..2] + (valid[2] == '9' ? '1' : (char)(valid[2] + 1)) + valid[3..];

        var good = Scanner.Scan(SecretTestItems.Note("IBAN " + valid));
        var bad = Scanner.Scan(SecretTestItems.Note("IBAN " + broken));

        good.ShouldHaveSingleItem().Kind.ShouldBe(SecretKind.Iban);
        bad.ShouldNotContain(finding => finding.Kind == SecretKind.Iban);
    }

    [Fact]
    public void Scan_bip39_phrase_with_valid_checksum_is_high_confidence_and_invalid_is_not()
    {
        string valid = Bip39Reference.Mnemonic(TestSecrets.Bytes(10, 16));
        string invalid = Bip39Reference.InvalidVariant(valid);

        var good = Scanner.Scan(SecretTestItems.Note("Wallet backup\n" + valid));
        var bad = Scanner.Scan(SecretTestItems.Note("Wallet backup\n" + invalid));

        var finding = good.ShouldHaveSingleItem();
        finding.Kind.ShouldBe(SecretKind.SeedPhrase);
        finding.Confidence.ShouldBe(Confidence.High);
        finding.ProposedTarget.ShouldBe(PlacementTarget.Hidden("Seed phrase"));
        bad.ShouldNotContain(found => found.Kind == SecretKind.SeedPhrase && found.Confidence >= Confidence.Medium);
    }

    [Fact]
    public void Scan_openssh_private_key_in_notes_proposes_ssh_key_item_with_computed_public_key()
    {
        var item = SecretTestItems.Login(name: "build server", notes: "deploy key\n" + PemFixtures.Ed25519OpenSsh() + "\nrotate yearly");

        var finding = Scanner.Scan(item).ShouldHaveSingleItem();
        var placed = SecretPlacer.Apply(item, [finding]);

        finding.Kind.ShouldBe(SecretKind.PrivateKey);
        finding.Confidence.ShouldBe(Confidence.High);
        finding.ProposedTarget.ShouldBe(PlacementTarget.NewSshKeyItem);
        var newItem = placed.NewItems.ShouldHaveSingleItem();
        newItem.Type.ShouldBe(VaultItemType.SshKey);
        newItem.Name.ShouldBe("build server SSH key");
        var key = newItem.SshKey.ShouldNotBeNull();
        key.PublicKey.ShouldBe(PemFixtures.Ed25519PublicKey);
        key.KeyFingerprint.ShouldBe(PemFixtures.Ed25519Fingerprint);
        placed.Item.Notes.ShouldBe("deploy key\nrotate yearly");
    }

    [Fact]
    public void Scan_pkcs1_rsa_key_is_converted_to_pkcs8_for_ssh_key_item()
    {
        using var rsa = RSA.Create(2048);
        var item = SecretTestItems.Note(PemFixtures.RsaPkcs1(rsa), "legacy host");

        var finding = Scanner.Scan(item).ShouldHaveSingleItem();
        var placed = SecretPlacer.Apply(item, [finding]);

        finding.ProposedTarget.ShouldBe(PlacementTarget.NewSshKeyItem);
        var key = placed.NewItems.ShouldHaveSingleItem().SshKey.ShouldNotBeNull();
        key.PrivateKey.ShouldStartWith(PemFixtures.Boundary("BEGIN", "PRIVATE KEY"), StringComparison.Ordinal);
        key.PublicKey.ShouldStartWith("ssh-rsa AAAAB3NzaC1yc2E", StringComparison.Ordinal);
        key.KeyFingerprint.ShouldStartWith("SHA256:", StringComparison.Ordinal);
        using var imported = RSA.Create();
        imported.ImportFromPem(key.PrivateKey);
        imported.ExportParameters(false).Modulus.ShouldBe(rsa.ExportParameters(false).Modulus);
    }

    [Theory]
    [InlineData("pkcs8")]
    [InlineData("proc-type")]
    [InlineData("openssh")]
    public void Scan_encrypted_private_key_is_flagged_not_converted(string variant)
    {
        using var rsa = RSA.Create(2048);
        string pem = variant switch
        {
            "pkcs8" => PemFixtures.EncryptedPkcs8(rsa),
            "proc-type" => PemFixtures.RsaPkcs1(rsa).Replace("KEY-----\n", "KEY-----\nProc-Type: 4,ENCRYPTED\nDEK-Info: AES-128-CBC,0123456789ABCDEF0123456789ABCDEF\n\n", StringComparison.Ordinal),
            _ => PemFixtures.EncryptedOpenSsh(),
        };
        var item = SecretTestItems.Note(pem);

        var finding = Scanner.Scan(item).ShouldHaveSingleItem();
        var placed = SecretPlacer.Apply(item, [finding]);

        finding.Kind.ShouldBe(SecretKind.PrivateKey);
        finding.NeedsReview.ShouldBeTrue();
        finding.IsPreAccepted.ShouldBeFalse();
        finding.ProposedTarget.ShouldBe(PlacementTarget.Hidden("Private key"));
        placed.NewItems.ShouldBeEmpty();
        placed.Item.Fields.ShouldHaveSingleItem().Value.ShouldBe(pem);
    }

    [Fact]
    public void Scan_ec_key_with_explicit_curve_parameters_in_notes_proposes_a_hidden_field_that_placement_fills_without_a_problem()
    {
        using var named = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var explicitCurve = ECDsa.Create(named.ExportExplicitParameters(includePrivateParameters: true));
        string pem = PemFixtures.Sec1(explicitCurve);
        var item = SecretTestItems.Note("deploy key\n" + pem, "build server");

        var finding = Scanner.Scan(item).ShouldHaveSingleItem();
        var placed = SecretPlacer.Apply(item, [finding]);

        finding.Kind.ShouldBe(SecretKind.PrivateKey);
        finding.NeedsReview.ShouldBeTrue();
        finding.IsPreAccepted.ShouldBeFalse();
        finding.ProposedTarget.ShouldBe(PlacementTarget.Hidden("Private key"));
        finding.Message.ShouldContain("unsupported ECDSA curve", StringComparison.Ordinal);
        placed.Problems.ShouldBeEmpty();
        placed.NewItems.ShouldBeEmpty();
        placed.Item.Fields.ShouldBe([new CustomField("Private key", pem, CustomFieldType.Hidden)]);
        placed.Item.Notes.ShouldBe("deploy key");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Scan_reports_nothing_for_a_key_that_fills_a_hidden_field_and_cannot_become_an_ssh_key_item(bool explicitCurveParameters)
    {
        using var named = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var explicitCurve = ECDsa.Create(named.ExportExplicitParameters(includePrivateParameters: true));
        string pem = explicitCurveParameters ? PemFixtures.Sec1(explicitCurve) : PemFixtures.EncryptedPkcs8(named);
        var item = SecretTestItems.Note("deploy key") with
        {
            Fields = [new CustomField("Deploy key", pem, CustomFieldType.Hidden)]
        };

        var findings = Scanner.Scan(item);

        findings.ShouldBeEmpty();
    }

    [Fact]
    public void Scan_still_proposes_an_ssh_key_item_for_a_convertible_key_that_fills_a_hidden_field()
    {
        var item = SecretTestItems.Note("deploy key") with
        {
            Fields = [new CustomField("Deploy key", PemFixtures.Ed25519OpenSsh(), CustomFieldType.Hidden)]
        };

        var finding = Scanner.Scan(item).ShouldHaveSingleItem();

        finding.Kind.ShouldBe(SecretKind.PrivateKey);
        finding.ProposedTarget.ShouldBe(PlacementTarget.NewSshKeyItem);
    }

    [Fact]
    public void Scan_github_token_with_valid_checksum_is_high_confidence()
    {
        string valid = TestSecrets.GitHubToken("ghp", 11);
        string invalid = valid[..^1] + (valid[^1] == 'A' ? 'B' : 'A');

        var good = Scanner.Scan(SecretTestItems.Note("deploy " + valid)).ShouldHaveSingleItem();
        var bad = Scanner.Scan(SecretTestItems.Note("deploy " + invalid)).ShouldHaveSingleItem();

        good.RuleId.ShouldBe("github-token");
        good.Confidence.ShouldBe(Confidence.High);
        good.IsPreAccepted.ShouldBeTrue();
        bad.Confidence.ShouldBe(Confidence.Low);
        bad.IsPreAccepted.ShouldBeFalse();
    }

    [Fact]
    public void Scan_postgres_uri_with_password_splits_into_login_fields()
    {
        const string Uri = "postgresql://app_user:s3cr3t-Pa55@db.internal.example:5432/orders?sslmode=require";
        var item = SecretTestItems.Login(name: "orders db", notes: "Primary database\n" + Uri);

        var finding = Scanner.Scan(item).ShouldHaveSingleItem();
        var placed = SecretPlacer.Apply(item, [finding]);

        finding.Kind.ShouldBe(SecretKind.ConnectionString);
        finding.ProposedTarget.ShouldBe(PlacementTarget.LoginFromConnection);
        var login = placed.Item.Login.ShouldNotBeNull();
        login.Username.ShouldBe("app_user");
        login.Password.ShouldBe("s3cr3t-Pa55");
        login.Uris.ShouldHaveSingleItem().Value.ShouldBe("postgresql://db.internal.example:5432/orders");
        var field = placed.Item.Fields.ShouldHaveSingleItem();
        field.Name.ShouldBe("Connection string");
        field.Value.ShouldBe(Uri);
        placed.Item.Notes.ShouldBe("Primary database");
    }

    [Fact]
    public void Scan_placeholders_and_uuids_are_rejected()
    {
        // Preserve the exact synthetic UUID rejection case without a credential-shaped source literal.
        string uuid = string.Join("-", "123e4567", "e89b", "12d3", "a456", "426614174000");
        string notes = $$$"""
            API key: changeme
            Token: <password>
            Secret: {{{uuid}}}
            Password: ***
            token: ${API_TOKEN}
            client secret: {{secret}}
            access key: %API_KEY%
            secret: true
            apikey: xxxxxxxxxxxxxxxx
            Released 2024-05-01T10:00:00Z as v1.2.3-beta.1 on 192.168.10.20
            Docs at https://example.com/docs/setup and /usr/local/share/app/config.json
            """;

        var findings = Scanner.Scan(SecretTestItems.Login(notes: notes));

        findings.ShouldBeEmpty();
    }

    [Fact]
    public void Scan_regex_timeout_produces_needs_review_finding()
    {
        var rule = new SecretRule
        {
            Id = "slow-rule",
            DisplayName = "Slow rule",
            Kind = SecretKind.ApiToken,
            Keywords = ["aaaa"],
            Pattern = SlowPatterns.Catastrophic(),
            Source = "test",
        };
        var scanner = new SecretScanner([rule]);

        var findings = scanner.Scan(SecretTestItems.Note(new string('a', 4000)));

        var finding = findings.ShouldHaveSingleItem(found => found.RuleId == "slow-rule");
        finding.Kind.ShouldBe(SecretKind.NeedsReview);
        finding.NeedsReview.ShouldBeTrue();
        finding.ProposedTarget.Kind.ShouldBe(PlacementTargetKind.NotMoved);
    }

    [Fact]
    public void Scan_regex_timeout_in_one_window_still_flags_the_later_windows_of_the_rule()
    {
        var rule = new SecretRule
        {
            Id = "slow-rule",
            DisplayName = "Slow rule",
            Kind = SecretKind.ApiToken,
            Keywords = ["aaaa"],
            Pattern = SlowPatterns.Catastrophic(),
            Source = "test",
        };
        var scanner = new SecretScanner([rule]);
        string run = new('a', 4000);
        const int SecondRun = 24_000;
        string notes = run + new string('x', SecondRun - run.Length) + run;

        var findings = scanner.Scan(SecretTestItems.Note(notes));

        SecretFinding[] timeouts = [.. findings.Where(found => found.RuleId == "slow-rule")];
        timeouts.ShouldAllSatisfy(found => found.Kind.ShouldBe(SecretKind.NeedsReview));
        timeouts.ShouldContain(found => found.Location.Start == 0);
        timeouts.ShouldContain(found => found.Location.Start < SecondRun && found.Location.Start + found.Location.Length >= notes.Length);
    }

    [Fact]
    public void Scan_runs_a_window_whose_match_timed_out_once_again_and_reports_its_finding()
    {
        int checks = 0;
        var rule = new SecretRule
        {
            Id = "paused-rule",
            DisplayName = "Paused rule",
            Kind = SecretKind.ApiToken,
            Keywords = ["tok_"],
            Pattern = TestPatterns.Token(),

            // The first check times out, as a match does when a pause of the process outlasts the match timeout.
            Validator = _ => ++checks == 1 ? throw new RegexMatchTimeoutException() : true,
            Source = "test",
        };
        var scanner = new SecretScanner([rule]);

        var findings = scanner.Scan(SecretTestItems.Note("deploy token tok_" + new string('k', 16)));

        var finding = findings.ShouldHaveSingleItem(found => found.RuleId == "paused-rule");
        finding.Kind.ShouldBe(SecretKind.ApiToken);
        finding.NeedsReview.ShouldBeFalse();
        checks.ShouldBe(2);
    }

    [Fact]
    public void Scan_flags_the_remaining_windows_of_a_rule_as_one_finding_once_three_of_its_windows_time_out()
    {
        // Runs of "a" 10,000 characters apart give the rule five separate windows: each run's window starts 1024 characters
        // before it, and the last one ends with the text.
        const int Spacing = 10_000;
        string run = new('a', 4000);
        string notes = string.Join(new string('x', Spacing - run.Length), Enumerable.Repeat(run, 5));
        var scanner = new SecretScanner([SlowPatterns.Rule()]);

        var findings = scanner.Scan(SecretTestItems.Note(notes));

        findings.ShouldAllSatisfy(found =>
        {
            found.RuleId.ShouldBe("slow-rule");
            found.Kind.ShouldBe(SecretKind.NeedsReview);
        });
        int[] starts = [.. findings.Select(found => found.Location.Start)];
        starts.ShouldBe([0, Spacing - 1024, (2 * Spacing) - 1024, (3 * Spacing) - 1024]);
        var rest = findings[^1];
        int restEnd = rest.Location.Start + rest.Location.Length;
        restEnd.ShouldBe(notes.Length);
        rest.Message.ShouldBe("Slow rule pattern timed out 3 times; review the rest of this text manually");
    }

    [Fact]
    public void Scan_bitwarden_account_recovery_code_is_flagged_not_moved()
    {
        string code = TestSecrets.FromAlphabet("abcdefghijklmnopqrstuvwxyz0123456789", 32, 12);
        string notes = "Recovery code: " + code;
        var item = SecretTestItems.Login(name: "Bitwarden", uri: "https://vault.bitwarden.com", notes: notes);

        var finding = Scanner.Scan(item).ShouldHaveSingleItem();
        var placed = SecretPlacer.Apply(item, [finding]);

        finding.Kind.ShouldBe(SecretKind.BitwardenRecoveryCode);
        finding.NeedsReview.ShouldBeTrue();
        finding.ProposedTarget.Kind.ShouldBe(PlacementTargetKind.NotMoved);
        placed.Item.Notes.ShouldBe(notes);
        placed.Item.Fields.ShouldBeEmpty();
        placed.Problems.ShouldHaveSingleItem().ShouldNotContain(code, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_otpauth_migration_payload_is_decoded()
    {
        byte[] first = TestSecrets.Bytes(20, 20);
        byte[] second = TestSecrets.Bytes(21, 20);
        string uri = MigrationPayloadBuilder.Uri(
            (first, "alice@example.com", "Example", 1, 1, 2),
            (second, "bob", "Other", 2, 2, 2),
            (TestSecrets.Bytes(22, 20), "counter", "Legacy", 1, 1, 1));
        var item = SecretTestItems.Login(notes: uri);

        var finding = Scanner.Scan(item).ShouldHaveSingleItem();
        var placed = SecretPlacer.Apply(item, [finding]);

        finding.Kind.ShouldBe(SecretKind.Totp);
        finding.RuleId.ShouldBe("otpauth-migration");
        string totp = placed.Item.Login.ShouldNotBeNull().Totp.ShouldNotBeNull();
        totp.ShouldBe("otpauth://totp/Example:alice%40example.com?secret=" + TestSecrets.Base32Encode(first) + "&issuer=Example");
        var extra = placed.Item.Fields.ShouldHaveSingleItem();
        extra.Name.ShouldBe("TOTP (2)");
        extra.Value.ShouldBe("otpauth://totp/Other:bob?secret=" + TestSecrets.Base32Encode(second) + "&issuer=Other&algorithm=SHA256&digits=8");
        placed.Problems.ShouldHaveSingleItem().ShouldContain("HOTP", StringComparison.Ordinal);
        placed.Item.Notes.ShouldBe(uri);
    }

    [Fact]
    public void Scan_prose_paragraphs_produce_nothing_above_suggestion()
    {
        string prose = Fixture.Read("secrets/prose.txt");

        var findings = Scanner.Scan(SecretTestItems.Note(prose));

        findings.ShouldAllSatisfy(finding => finding.Confidence.ShouldBe(Confidence.Suggestion));
    }

    [Fact]
    public void Scan_luhn_valid_card_number_in_text_field_is_reported_as_misfiled()
    {
        string number = TestSecrets.CardNumber("51", 16);
        var item = SecretTestItems.Login(fields: [new CustomField("Card", number, CustomFieldType.Text)]);

        var finding = Scanner.Scan(item).ShouldHaveSingleItem();
        var placed = SecretPlacer.Apply(item, [finding]);

        finding.Location.FieldPath.ShouldBe("fields[0].value");
        finding.ProposedTarget.ShouldBe(PlacementTarget.HiddenNamedByLabel("Card number"));
        var field = placed.Item.Fields.ShouldHaveSingleItem();
        field.Name.ShouldBe("Card");
        field.Type.ShouldBe(CustomFieldType.Hidden);
        field.Value.ShouldBe(number);
    }

    [Fact]
    public void Scan_pin_in_password_field_of_bank_item_is_reported_as_misfiled()
    {
        var item = SecretTestItems.Login(name: "Sparkasse Bank card", password: "4821");

        var finding = Scanner.Scan(item).ShouldHaveSingleItem();

        finding.Kind.ShouldBe(SecretKind.Pin);
        finding.Location.FieldPath.ShouldBe(SecretLocation.LoginPassword);
        finding.ProposedTarget.ShouldBe(PlacementTarget.Hidden("PIN"));
    }

    [Fact]
    public void Scan_text_recovery_code_file_name_raises_confidence_for_digit_codes()
    {
        string codes = string.Join('\n', Enumerable.Range(0, 10).Select(index => TestSecrets.FromAlphabet("0123456789", 8, 300 + index)));

        var withName = Scanner.ScanText(codes, SecretLocation.Attachment("Backup-codes-alice.txt"), "Backup-codes-alice.txt");
        var withoutName = Scanner.ScanText(codes, SecretLocation.Attachment("numbers.txt"), "numbers.txt");

        var finding = withName.ShouldHaveSingleItem();
        finding.Kind.ShouldBe(SecretKind.RecoveryCodes);
        finding.Location.FieldPath.ShouldBe("attachment:Backup-codes-alice.txt");
        withoutName.ShouldNotContain(found => found.Kind == SecretKind.RecoveryCodes);
    }
}
