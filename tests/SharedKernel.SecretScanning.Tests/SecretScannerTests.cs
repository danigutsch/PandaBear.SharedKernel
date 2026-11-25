namespace PandaBear.SecretScanning.Tests;

/// <summary>Tests what the scanner reports, what it hands to a projection, and how the context steers it.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.SecretScanningCapability)]
public sealed class SecretScannerTests
{
    [Fact]
    public void A_found_secret_reports_where_it_is_and_never_its_value()
    {
        // Arrange
        string jwt = TestSecrets.Jwt(119);
        string text = "session token " + jwt + "\n";

        // Act
        var finding = SecretScanner.Default.Scan(text, "notes", SecretScanContext.None).ShouldHaveSingleItem(found => found.RuleId == "jwt");

        // Assert
        text.Substring(finding.Location.Start, finding.Location.Length).ShouldBe(jwt);
        finding.Location.FieldPath.ShouldBe("notes");
        finding.RuleName.ShouldNotBeNull();
        finding.ToString().ShouldNotContain(jwt, StringComparison.Ordinal);
        finding.Message.ShouldNotContain(jwt, StringComparison.Ordinal);
    }

    [Fact]
    public void A_projection_receives_each_found_value()
    {
        // Arrange
        string jwt = TestSecrets.Jwt(119);

        // Act
        var values = SecretScanner.Default.Scan("token " + jwt, "notes", SecretScanContext.None, static (finding, value) => (finding.RuleId, value));

        // Assert
        values.ShouldContain(("jwt", jwt));
    }

    [Fact]
    public void A_region_where_a_pattern_timed_out_has_no_single_value()
    {
        // Arrange
        var scanner = new SecretScanner([SlowPatterns.Rule()]);

        // Act
        var values = scanner.Scan(new string('a', 4000), "notes", SecretScanContext.None, static (finding, value) => (finding.TimedOut, value));

        // Assert
        values.ShouldNotBeEmpty();
        values.ShouldAllSatisfy(found =>
        {
            found.TimedOut.ShouldBeTrue();
            found.value.ShouldBeNull();
        });
    }

    [Fact]
    public void A_labeled_user_name_is_reported_only_when_the_account_expects_one()
    {
        // Arrange
        const string Text = "username: alice.example\n";

        // Act
        var expected = SecretScanner.Default.Scan(Text, "notes", new SecretScanContext { ExpectsUsername = true });
        var unexpected = SecretScanner.Default.Scan(Text, "notes", SecretScanContext.None);

        // Assert
        expected.ShouldContain(found => found.RuleId == "notes-username");
        unexpected.ShouldNotContain(found => found.RuleId == "notes-username");
    }

    [Fact]
    public void A_bitwarden_recovery_code_is_recognised_only_in_a_bitwarden_account()
    {
        // Arrange
        string text = "recovery code: " + string.Concat(Enumerable.Repeat("k3m9", 8)) + "\n";

        // Act
        var bitwarden = SecretScanner.Default.Scan(text, "notes", new SecretScanContext { IsBitwardenAccount = true });
        var other = SecretScanner.Default.Scan(text, "notes", SecretScanContext.None);

        // Assert
        bitwarden.ShouldContain(found => found.Kind == SecretKind.BitwardenRecoveryCode && found.NeedsReview);
        other.ShouldNotContain(found => found.Kind == SecretKind.BitwardenRecoveryCode);
    }

    [Fact]
    public void Catalog_rules_only_skips_the_structural_detectors()
    {
        // Arrange
        string text = "PIN: 4821\ntoken " + TestSecrets.Jwt(119);

        // Act
        var all = SecretScanner.Default.Scan(text, "notes", SecretScanContext.None);
        var rulesOnly = SecretScanner.Default.Scan(text, "notes", new SecretScanContext { CatalogRulesOnly = true });

        // Assert
        all.ShouldContain(found => found.RuleId == "notes-pin");
        rulesOnly.ShouldNotContain(found => found.RuleId == "notes-pin");
        rulesOnly.ShouldContain(found => found.RuleId == "jwt");
    }

    [Fact]
    public void Catalog_rules_only_returns_the_findings_in_the_order_of_the_text()
    {
        // Arrange
        string text = "session " + TestSecrets.Jwt(119) + "\ngithub " + TestSecrets.GitHubToken("ghp", 1) + "\n";

        // Act
        var findings = SecretScanner.Default.Scan(text, "notes", new SecretScanContext { CatalogRulesOnly = true });

        // Assert
        findings.Select(found => found.RuleId).ShouldBe(["jwt", "github-token"]);
    }

    [Fact]
    public void A_steam_authenticator_file_returns_its_findings_in_the_order_of_the_text()
    {
        // Arrange
        string sharedSecret = Convert.ToBase64String(TestSecrets.Bytes(7, 20));
        string text = "{\"revocation_code\":\"R12345\",\"shared_secret\":\"" + sharedSecret + "\"}";

        // Act
        var findings = SecretScanner.Default.Scan(text, "attachment:76561197960287930.maFile", new SecretScanContext { FileName = "76561197960287930.maFile" });

        // Assert
        findings.Select(found => found.RuleId).ShouldBe(["steam-mafile-revocation-code", SecretScanner.MaFileSharedSecretRule]);
    }

    [Fact]
    public void A_mega_recovery_key_file_is_recognised_by_its_name()
    {
        // Arrange
        const string Key = "AbCdEfGhIjKlMnOpQrStUv";

        // Act
        var named = SecretScanner.Default.Scan(Key + "\n", "attachment:MEGA-RECOVERYKEY.txt", new SecretScanContext { FileName = "MEGA-RECOVERYKEY.txt" });
        var unnamed = SecretScanner.Default.Scan(Key + "\n", "notes", SecretScanContext.None);

        // Assert
        named.ShouldContain(found => found.RuleId == "mega-recovery-key" && found.Kind == SecretKind.RecoveryCodes);
        unnamed.ShouldNotContain(found => found.RuleId == "mega-recovery-key");
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        // Arrange
        var scanner = SecretScanner.Default;

        // Act
        var calls = new Action[]
        {
            () => scanner.Scan(null!, "notes", SecretScanContext.None),
            () => scanner.Scan("text", null!, SecretScanContext.None),
            () => scanner.Scan("text", "notes", null!),
            () => scanner.Scan<int>("text", "notes", SecretScanContext.None, null!),
        };

        // Assert
        foreach (var call in calls)
        {
            call.ShouldThrow<ArgumentNullException>();
        }
    }

    [Fact]
    public void A_parsed_pem_block_never_prints_its_key_material()
    {
        // Arrange
        string pem = PemFixtures.Ed25519OpenSsh();

        // Act
        PemBlock.TryParse(pem, out PemBlock? block).ShouldBeTrue();

        // Assert
        var parsed = block.ShouldNotBeNull();
        parsed.ToString().ShouldNotContain(parsed.Body[..32], StringComparison.Ordinal);
        parsed.ToString().ShouldContain("OPENSSH PRIVATE KEY", StringComparison.Ordinal);
    }
}
