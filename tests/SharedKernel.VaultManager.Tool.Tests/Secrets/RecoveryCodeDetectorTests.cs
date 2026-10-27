using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>Tests how the secret scanner detects blocks of recovery codes.</summary>
public sealed class RecoveryCodeDetectorTests
{
    /// <summary>Google recovery codes in two columns separated by spaces are detected as one high-confidence block.</summary>
    [Fact]
    public void Google_codes_in_two_columns_with_spaces_are_one_block()
    {
        // Arrange
        var lines = Enumerable.Range(0, 5).Select(index =>
            $"{TestSecrets.SpacedDigitCode(index, 0)}    {TestSecrets.SpacedDigitCode(index, 1)}");
        string notes = "Google backup codes\nKeep these somewhere safe.\n" + string.Join('\n', lines);
        var item = SecretTestItems.Login(notes: notes, uri: "https://accounts.google.com");

        // Act
        var findings = SecretScanner.Default.Scan(item);

        // Assert
        var finding = findings.ShouldHaveSingleItem();
        finding.Kind.ShouldBe(SecretKind.RecoveryCodes);
        finding.Confidence.ShouldBe(Confidence.High);
    }

    /// <summary>Numbered recovery codes with used markers are detected as one block that spans the numbered lines.</summary>
    [Fact]
    public void Numbered_codes_with_used_markers_are_detected()
    {
        // Arrange
        var lines = Enumerable.Range(0, 10).Select(index =>
            $"{index + 1}. {TestSecrets.FromAlphabet(TestSecrets.LowerHex, 16, 200 + index)}{(index < 2 ? " (used)" : string.Empty)}");
        var item = SecretTestItems.Login(notes: "GitLab recovery codes:\n" + string.Join('\n', lines), uri: "https://gitlab.com");

        // Act
        var findings = SecretScanner.Default.Scan(item);

        // Assert
        var finding = findings.ShouldHaveSingleItem();
        finding.Kind.ShouldBe(SecretKind.RecoveryCodes);
        finding.Location.FirstLine.ShouldBe(2);
        finding.Location.LastLine.ShouldBe(11);
    }

    /// <summary>Dates, phone numbers, amounts, and number sequences are not mistaken for recovery codes.</summary>
    [Theory]
    [InlineData("2024-01-0{0}")]
    [InlineData("+49 30 1234567{0}")]
    [InlineData("$1,234.5{0}")]
    [InlineData("1234567{0}")]
    public void Dates_phone_numbers_amounts_and_sequences_are_not_codes(string pattern)
    {
        // Arrange
        var lines = Enumerable.Range(1, 8).Select(index => string.Format(System.Globalization.CultureInfo.InvariantCulture, pattern, index));
        var item = SecretTestItems.Note("Backup codes\n" + string.Join('\n', lines));

        // Act
        var findings = SecretScanner.Default.Scan(item);

        // Assert
        findings.ShouldNotContain(finding => finding.Kind == SecretKind.RecoveryCodes);
    }

    /// <summary>Four codes, or codes of mixed shapes, are not reported as a recovery code block.</summary>
    [Fact]
    public void Four_codes_or_mixed_shapes_are_not_a_block()
    {
        // Arrange
        string four = string.Join('\n', Enumerable.Range(0, 4).Select(index => TestSecrets.FromAlphabet(TestSecrets.LowerHex, 10, index)));
        string mixed = string.Join('\n', Enumerable.Range(0, 8).Select(index => TestSecrets.FromAlphabet(TestSecrets.LowerHex, 8 + index, index)));

        // Act
        var fourFindings = SecretScanner.Default.Scan(SecretTestItems.Note("recovery codes\n" + four));
        var mixedFindings = SecretScanner.Default.Scan(SecretTestItems.Note("recovery codes\n" + mixed));

        // Assert
        fourFindings.ShouldNotContain(finding => finding.Kind == SecretKind.RecoveryCodes);
        mixedFindings.ShouldNotContain(finding => finding.Kind == SecretKind.RecoveryCodes);
    }

    /// <summary>A single Microsoft recovery code under a recovery key heading is detected and proposed for a hidden field.</summary>
    [Fact]
    public void Single_microsoft_recovery_code_under_recovery_key_is_detected()
    {
        // Arrange
        string code = string.Join('-', Enumerable.Range(0, 5).Select(index => TestSecrets.FromAlphabet("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 5, 210 + index)));
        var item = SecretTestItems.Login(notes: "Recovery code: " + code, uri: "https://account.microsoft.com");

        // Act
        var findings = SecretScanner.Default.Scan(item);

        // Assert
        var finding = findings.ShouldHaveSingleItem();
        finding.Kind.ShouldBe(SecretKind.RecoveryCodes);
        finding.ProposedTarget.ShouldBe(PlacementTarget.Hidden("Recovery codes"));
    }

    /// <summary>A Steam maFile yields its shared secret as a TOTP finding and its revocation code as a recovery code finding.</summary>
    [Fact]
    public void Steam_mafile_shared_secret_becomes_steam_totp()
    {
        // Arrange
        string shared = Convert.ToBase64String(TestSecrets.Bytes(220, 20));
        string text = "{\"shared_secret\":\"" + shared + "\",\"account_name\":\"gamer\",\"revocation_code\":\"R12345\"}";

        // Act
        var findings = SecretScanner.Default.ScanText(text, SecretLocation.Attachment("76561198000000000.maFile"), "76561198000000000.maFile");

        // Assert
        findings.ShouldContain(finding => finding.Kind == SecretKind.Totp && finding.RuleId == "steam-mafile-shared-secret");
        findings.ShouldContain(finding => finding.Kind == SecretKind.RecoveryCodes && finding.RuleId == "steam-mafile-revocation-code");
    }
}
