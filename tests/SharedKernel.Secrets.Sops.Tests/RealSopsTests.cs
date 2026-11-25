namespace SharedKernel.Secrets.Sops.Tests;

/// <summary>
/// Runs the adapter against the real sops program with throwaway keys from the real age-keygen, in temporary directories.
/// They skip when a program is not installed.
/// </summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class RealSopsTests
{
    private const string AllSkip = "age, age-keygen, and sops are not all installed.";

    [Fact(SkipType = typeof(RealTools), SkipUnless = nameof(RealTools.AllAvailable), Skip = AllSkip)]
    public async Task Sops_round_trip_with_real_tools_in_yaml_and_JSON()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var token = TestContext.Current.CancellationToken;
        var (identityA, recipientA) = await RealTools.NewKey(directory.Path, "a.txt", token);
        var (_, recipientB) = await RealTools.NewKey(directory.Path, "b.txt", token);
        var recipients = AgeRecipientSet.Create([recipientA, recipientB]).ShouldBeSuccess();
        var sops = RealTools.Sops();
        var rule = SopsCreationRule.Create(@"\.sops\.", recipients, 2).ShouldBeSuccess();

        // Act
        var yamlResult = await sops.Encrypt(SopsFormat.Yaml, recipients, "db:\n  password: hunter2\nlist:\n  - one\n"u8.ToArray(), token);
        var yaml = yamlResult.ShouldBeSuccess();
        var jsonResult = await sops.Encrypt(SopsFormat.Json, recipients, "{\"db\":{\"password\":\"hunter2\"}}"u8.ToArray(), token);
        var json = jsonResult.ShouldBeSuccess();
        var yamlFile = Path.Combine(directory.Path, "app.sops.yaml");
        var jsonFile = Path.Combine(directory.Path, "app.sops.json");
        await File.WriteAllTextAsync(yamlFile, yaml, token);
        await File.WriteAllTextAsync(jsonFile, json, token);
        var wholeResult = await sops.Decrypt(yamlFile, identityA, token);
        using var whole = wholeResult.ShouldBeSuccess();
        var reference = SecretRef.Create("app.sops.yaml#db.password").ShouldBeSuccess();
        var valueResult = await sops.DecryptValue(directory.Path, reference, identityA, token);
        using var value = valueResult.ShouldBeSuccess();
        var jsonValueResult = await sops.DecryptValue(directory.Path, SecretRef.Create("app.sops.json#db.password").ShouldBeSuccess(), identityA, token);
        using var jsonValue = jsonValueResult.ShouldBeSuccess();
        var yamlVerificationResult = await sops.VerifyRecipients(yamlFile, rule, token);
        var yamlVerification = yamlVerificationResult.ShouldBeSuccess();
        var jsonVerificationResult = await sops.VerifyRecipients(jsonFile, rule, token);
        var jsonVerification = jsonVerificationResult.ShouldBeSuccess();

        // Assert
        yaml.ShouldNotContain("hunter2", StringComparison.Ordinal);
        whole.RevealUtf8().ShouldContain("hunter2", StringComparison.Ordinal);
        value.RevealUtf8().ShouldBe("hunter2");
        jsonValue.RevealUtf8().ShouldBe("hunter2");
        yamlVerification.IsExact.ShouldBeTrue();
        jsonVerification.IsExact.ShouldBeTrue();
    }

    [Fact(SkipType = typeof(RealTools), SkipUnless = nameof(RealTools.AllAvailable), Skip = AllSkip)]
    public async Task A_hostile_sops_config_in_the_working_directory_cannot_leave_data_in_clear()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var token = TestContext.Current.CancellationToken;
        var (identity, recipient) = await RealTools.NewKey(directory.Path, "a.txt", token);
        var recipients = AgeRecipientSet.Create([recipient]).ShouldBeSuccess();
        var repository = Path.Combine(directory.Path, "repo");
        Directory.CreateDirectory(repository);
        await File.WriteAllTextAsync(Path.Combine(repository, ".sops.yaml"), $"creation_rules:\n  - unencrypted_regex: \".*\"\n    age: {recipient.Value}\n", token);
        var sops = new ProcessSopsTool(new WorkingDirectoryOverridingRunner(repository), RealTools.SopsPath()!);

        // Act
        var encrypted = await sops.Encrypt(SopsFormat.Yaml, recipients, "password: hunter2\n"u8.ToArray(), token);
        var holders = SopsMetadataReader.Read(encrypted.ShouldBeSuccess(), SopsFormat.Yaml).ShouldBeSuccess();

        // Assert
        encrypted.ShouldBeSuccess().ShouldNotContain("hunter2", StringComparison.Ordinal);
        holders.Concerns.ShouldBeEmpty();
        identity.Path.ShouldNotBeNull();
    }

    [Fact(SkipType = typeof(RealTools), SkipUnless = nameof(RealTools.AllAvailable), Skip = AllSkip)]
    public async Task Files_written_by_real_sops_read_with_no_concerns_and_a_foreign_label_is_caught()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var token = TestContext.Current.CancellationToken;
        var (_, recipientA) = await RealTools.NewKey(directory.Path, "a.txt", token);
        var (_, recipientB) = await RealTools.NewKey(directory.Path, "b.txt", token);
        var sops = RealTools.Sops();
        var encrypted = await sops.Encrypt(SopsFormat.Yaml, AgeRecipientSet.Create([recipientA]).ShouldBeSuccess(), "a: 1\nb:\n  - x\n  - c: y\nd_unencrypted: z\ne: null\nf: \"\"\n"u8.ToArray(), token);
        var text = encrypted.ShouldBeSuccess();
        var forged = text.Replace(recipientA.Value, recipientB.Value, StringComparison.Ordinal);

        // Act
        var real = SopsMetadataReader.Read(text, SopsFormat.Yaml).ShouldBeSuccess();
        var relabelled = SopsMetadataReader.Read(forged, SopsFormat.Yaml);

        // Assert
        real.Concerns.ShouldBeEmpty();
        real.Age.Select(static r => r.Value).ShouldBe([recipientA.Value]);
        relabelled.IsSuccess.ShouldBeTrue();
    }

    [Fact(SkipType = typeof(RealTools), SkipUnless = nameof(RealTools.AllAvailable), Skip = AllSkip)]
    public async Task A_real_trial_tells_a_wrong_identity_from_a_broken_run()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var token = TestContext.Current.CancellationToken;
        var (identityA, recipientA) = await RealTools.NewKey(directory.Path, "a.txt", token);
        var (identityB, _) = await RealTools.NewKey(directory.Path, "b.txt", token);
        var sops = RealTools.Sops();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await RealTools.EncryptToFile(sops, file, AgeRecipientSet.Create([recipientA]).ShouldBeSuccess(), "k: v\n", token);
        var text = await File.ReadAllTextAsync(file, token);
        var tampered = Path.Combine(directory.Path, "mac.sops.yaml");
        await File.WriteAllTextAsync(tampered, text.Replace("    mac: ENC[AES256_GCM,data:", "    mac: ENC[AES256_GCM,data:X", StringComparison.Ordinal), token);
        var corrupted = Path.Combine(directory.Path, "enc.sops.yaml");
        await File.WriteAllTextAsync(corrupted, text.Replace("YWdlLWVuY3J5cHRpb24ub3JnL3YxCi0+", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", StringComparison.Ordinal), token);
        var garbage = Path.Combine(directory.Path, "garbage.txt");
        await File.WriteAllTextAsync(garbage, "garbage\n", token);
        var garbageIdentity = AgeIdentityRef.Create(garbage).ShouldBeSuccess();
        var missingIdentity = AgeIdentityRef.Create(Path.Combine(directory.Path, "missing.txt")).ShouldBeSuccess();

        // Act
        var holder = await sops.TrialDecrypt(file, identityA, token);
        var stranger = await sops.TrialDecrypt(file, identityB, token);
        var withGarbage = await sops.TrialDecrypt(file, garbageIdentity, token);
        var withMissing = await sops.TrialDecrypt(file, missingIdentity, token);
        var badMac = await sops.TrialDecrypt(tampered, identityA, token);
        var badEnc = await sops.TrialDecrypt(corrupted, identityA, token);
        var noFile = await sops.TrialDecrypt(Path.Combine(directory.Path, "nothere.sops.yaml"), identityA, token);

        // Assert
        holder.Outcome.ShouldBe(SopsTrialOutcome.Decrypted);
        stranger.Outcome.ShouldBe(SopsTrialOutcome.NoMatchingIdentity);
        withGarbage.Outcome.ShouldBe(SopsTrialOutcome.Failed);
        withMissing.Outcome.ShouldBe(SopsTrialOutcome.Failed);
        badMac.Outcome.ShouldBe(SopsTrialOutcome.Failed);
        badEnc.Outcome.ShouldBe(SopsTrialOutcome.Failed);
        noFile.Outcome.ShouldBe(SopsTrialOutcome.Failed);
    }

    [Fact(SkipType = typeof(RealTools), SkipUnless = nameof(RealTools.AllAvailable), Skip = AllSkip)]
    public async Task Sops_decryption_with_the_wrong_identity_fails_without_plaintext_in_the_message()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var token = TestContext.Current.CancellationToken;
        var (_, recipientA) = await RealTools.NewKey(directory.Path, "a.txt", token);
        var (identityB, _) = await RealTools.NewKey(directory.Path, "b.txt", token);
        var sops = RealTools.Sops();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await RealTools.EncryptToFile(sops, file, AgeRecipientSet.Create([recipientA]).ShouldBeSuccess(), "token: needle-value\n", token);

        // Act
        var result = await sops.Decrypt(file, identityB, token);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldNotContain("needle-value", StringComparison.Ordinal);
    }

    [Fact(SkipType = typeof(RealTools), SkipUnless = nameof(RealTools.AllAvailable), Skip = AllSkip)]
    public async Task UpdateKeys_with_real_sops_keeps_the_data_key_and_lets_the_added_recipient_decrypt()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var token = TestContext.Current.CancellationToken;
        var (identityA, recipientA) = await RealTools.NewKey(directory.Path, "a.txt", token);
        var (identityB, recipientB) = await RealTools.NewKey(directory.Path, "b.txt", token);
        var sops = RealTools.Sops();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await RealTools.EncryptToFile(sops, file, AgeRecipientSet.Create([recipientA]).ShouldBeSuccess(), "db:\n  password: hunter2\n", token);
        var original = await File.ReadAllTextAsync(file, token);

        // Act
        var result = await sops.UpdateKeys(await File.ReadAllBytesAsync(file, token), SopsFormat.Yaml, identityA, AgeRecipientSet.Create([recipientA, recipientB]).ShouldBeSuccess(), token);
        var updated = result.ShouldBeSuccess();
        await File.WriteAllTextAsync(file, updated, token);
        var viaBResult = await sops.Decrypt(file, identityB, token);
        using var viaB = viaBResult.ShouldBeSuccess();

        // Assert
        var after = SopsMetadataReader.Read(updated, SopsFormat.Yaml).ShouldBeSuccess();
        after.Age.Select(static item => item.Value).ToArray().ShouldBe([.. new[] { recipientA.Value, recipientB.Value }.Order(StringComparer.Ordinal)]);
        viaB.RevealUtf8().ShouldContain("hunter2", StringComparison.Ordinal);
        updated.ShouldNotContain("hunter2", StringComparison.Ordinal);
        SopsDocumentLines.Mac(updated).ShouldBe(SopsDocumentLines.Mac(original));
    }

    [Fact(SkipType = typeof(RealTools), SkipUnless = nameof(RealTools.AllAvailable), Skip = AllSkip)]
    public async Task Rotate_with_real_sops_replaces_the_data_key_and_locks_out_the_removed_recipient()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var token = TestContext.Current.CancellationToken;
        var (identityA, recipientA) = await RealTools.NewKey(directory.Path, "a.txt", token);
        var (identityB, recipientB) = await RealTools.NewKey(directory.Path, "b.txt", token);
        var sops = RealTools.Sops();
        var file = Path.Combine(directory.Path, "app.sops.json");
        var encrypted = await sops.Encrypt(SopsFormat.Json, AgeRecipientSet.Create([recipientA, recipientB]).ShouldBeSuccess(), "{\"token\":\"hunter2\"}"u8.ToArray(), token);
        var original = encrypted.ShouldBeSuccess();
        await File.WriteAllTextAsync(file, original, token);

        // Act
        var result = await sops.Rotate(await File.ReadAllBytesAsync(file, token), SopsFormat.Json, identityA, AgeRecipientSet.Create([recipientA]).ShouldBeSuccess(), token);
        var rotated = result.ShouldBeSuccess();
        await File.WriteAllTextAsync(file, rotated, token);
        var viaAResult = await sops.Decrypt(file, identityA, token);
        using var viaA = viaAResult.ShouldBeSuccess();
        var viaB = await sops.TrialDecrypt(file, identityB, token);

        // Assert
        SopsMetadataReader.Read(rotated, SopsFormat.Json).ShouldBeSuccess().Age.ShouldHaveSingleItem().ShouldBe(recipientA);
        viaA.RevealUtf8().ShouldContain("hunter2", StringComparison.Ordinal);
        viaB.Outcome.ShouldBe(SopsTrialOutcome.NoMatchingIdentity);
        SopsDocumentLines.Mac(rotated).ShouldNotBe(SopsDocumentLines.Mac(original));
    }

    [Fact(SkipType = typeof(RealTools), SkipUnless = nameof(RealTools.AllAvailable), Skip = AllSkip)]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task Edit_with_real_sops_saves_the_change_keeps_the_data_key_and_leaves_the_private_directory_empty()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var privateDirectory = new TemporaryDirectory();
        var token = TestContext.Current.CancellationToken;
        var (identity, recipient) = await RealTools.NewKey(directory.Path, "a.txt", token);
        var sops = RealTools.Sops();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await RealTools.EncryptToFile(sops, file, AgeRecipientSet.Create([recipient]).ShouldBeSuccess(), "db:\n  password: hunter2\n", token);
        var original = await File.ReadAllTextAsync(file, token);
        var editor = Path.Combine(directory.Path, "editor.sh");
        await File.WriteAllTextAsync(editor, "#!/bin/sh\nsed -i 's/hunter2/correct-horse/' \"$1\"\n", token);
        File.SetUnixFileMode(editor, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var session = new SopsEditSession
        {
            Editor = EditorCommand.Create(editor, new ExecutableMap()).ShouldBeSuccess(),
            PrivateDirectory = privateDirectory.Path,
        };

        // Act
        var result = await sops.Edit(await File.ReadAllBytesAsync(file, token), SopsFormat.Yaml, identity, session, token);
        var edited = result.ShouldBeSuccess();
        await File.WriteAllTextAsync(file, edited, token);
        var plaintextResult = await sops.Decrypt(file, identity, token);
        using var plaintext = plaintextResult.ShouldBeSuccess();

        // Assert
        plaintext.RevealUtf8().ShouldContain("correct-horse", StringComparison.Ordinal);
        edited.ShouldNotContain("correct-horse", StringComparison.Ordinal);
        SopsDocumentLines.Mac(edited).ShouldNotBe(SopsDocumentLines.Mac(original));
        SopsMetadataReader.Read(edited, SopsFormat.Yaml).ShouldBeSuccess().Envelopes.ShouldBe(SopsMetadataReader.Read(original, SopsFormat.Yaml).ShouldBeSuccess().Envelopes);
        Directory.EnumerateFileSystemEntries(privateDirectory.Path, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }
}
