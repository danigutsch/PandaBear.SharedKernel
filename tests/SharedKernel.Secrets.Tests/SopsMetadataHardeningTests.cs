using System.Text;

namespace PandaBear.Secrets.Tests;

/// <summary>Tests the inputs of a security review: files that hide the real metadata, forge labels, or leave data in clear must not read as exact.</summary>
[Trait("Capability", "Secrets")]
public sealed class SopsMetadataHardeningTests
{
    private static readonly string[] Both = [TestRecipients.First, TestRecipients.Second];
    private static readonly SopsCreationRule AllowedOnly = TestRecipients.Rule("x", 1, TestRecipients.First);

    [Fact]
    public void A_second_yaml_document_with_a_fake_sops_block_cannot_hide_the_real_one()
    {
        // Arrange
        var yaml = SopsSamples.Yaml(Both) + "---\n" + SopsSamples.FakeBlock(TestRecipients.First);

        // Act
        var result = SopsMetadataReader.Read(yaml, SopsFormat.Yaml);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("---\n")]
    [InlineData("--- \n")]
    [InlineData("---")]
    [InlineData("...\n")]
    [InlineData("%YAML 1.2\n")]
    public void Document_markers_and_directives_are_refused_before_or_after_the_block(string marker)
    {
        // Act
        var before = SopsMetadataReader.Read(marker + SopsSamples.Yaml(Both), SopsFormat.Yaml);
        var after = SopsMetadataReader.Read(SopsSamples.Yaml(Both) + marker, SopsFormat.Yaml);

        // Assert
        before.IsFailure.ShouldBeTrue();
        after.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void A_byte_order_mark_before_the_block_is_refused()
    {
        // Arrange
        var yaml = "\uFEFF" + SopsSamples.Yaml(Both) + "---\n" + SopsSamples.FakeBlock(TestRecipients.First);

        // Act
        var result = SopsMetadataReader.Read(yaml, SopsFormat.Yaml);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("sops :\n")]
    [InlineData("sops\t:\n")]
    [InlineData("\"sops\":\n")]
    [InlineData("'sops':\n")]
    [InlineData("\"sops\" :\n")]
    [InlineData("? sops\n:\n")]
    [InlineData("sops : # c\n")]
    public void A_top_level_key_that_reads_as_sops_but_is_not_spelled_exactly_so_is_refused(string spelling)
    {
        // Arrange
        var yaml = $"password: ENC[AES256_GCM,data:x]\n{spelling}" + string.Concat(SopsSamples.FakeBlock(TestRecipients.First).Split('\n').Skip(1).Select(static l => l + "\n")).TrimEnd('\n') + "\n---\n" + SopsSamples.FakeBlock(TestRecipients.First);

        // Act
        var result = SopsMetadataReader.Read(yaml, SopsFormat.Yaml);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("a: &x 1\n")]
    [InlineData("b: *x\n")]
    [InlineData("c: {a: 1}\n")]
    [InlineData("d: [1, 2]\n")]
    [InlineData("- item\n")]
    [InlineData("<<: *base\n")]
    [InlineData("!!map\n")]
    [InlineData("just text\n")]
    [InlineData("\tindented: 1\n")]
    [InlineData("e: !tag v\n")]
    public void Top_level_constructs_the_reader_does_not_understand_are_refused(string line)
    {
        // Act
        var result = SopsMetadataReader.Read(line + SopsSamples.Yaml(Both), SopsFormat.Yaml);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("sops :")]
    [InlineData("\"sops\":")]
    [InlineData("'sops':")]
    [InlineData("SOPS:")]
    [InlineData("sops:\t")]
    public void A_sops_key_spelled_any_other_way_is_refused_even_with_no_second_document(string spelling)
    {
        // Arrange
        var yaml = SopsSamples.Yaml(Both).Replace("sops:\n", spelling + "\n", StringComparison.Ordinal);

        // Act
        var result = SopsMetadataReader.Read(yaml, SopsFormat.Yaml);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void A_byte_order_mark_alone_is_refused()
    {
        // Act
        var result = SopsMetadataReader.Read("\uFEFF" + SopsSamples.Yaml(Both), SopsFormat.Yaml);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("a: &x 1\nb: *x\n")]
    [InlineData("<<: *x\n")]
    [InlineData("c: !!str v\n")]
    [InlineData("d: {x: 1}\n")]
    public void Top_level_anchors_aliases_merges_tags_and_flow_mappings_are_refused_on_their_own(string lines)
    {
        // Act
        var result = SopsMetadataReader.Read(SopsSamples.Yaml(Both, dataLines: lines), SopsFormat.Yaml);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Two_sops_blocks_in_one_document_are_refused()
    {
        // Act
        var result = SopsMetadataReader.Read(SopsSamples.Yaml(Both) + SopsSamples.FakeBlock(TestRecipients.First), SopsFormat.Yaml);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("\nsops:", "\n!!binary c29wcw==:")]
    [InlineData("\nsops:", "\n!<tag:yaml.org,2002:binary> c29wcw==:")]
    [InlineData("\nsops:", "\n!!str sops:")]
    [InlineData("password: ", "password: !!binary ")]
    [InlineData("    version: 3.13.0", "    version: !!str 3.13.0")]
    [InlineData("        - recipient: ", "        - !!str recipient: ")]
    [InlineData("    kms: []", "    kms: [!!str a]")]
    public void A_yaml_tag_anywhere_in_the_metadata_or_data_is_refused_with_a_clear_message(string target, string replacement)
    {
        // Arrange
        var yaml = SopsSamples.Yaml(Both).Replace(target, replacement, StringComparison.Ordinal);

        // Act
        var result = SopsMetadataReader.Read(yaml, SopsFormat.Yaml);

        // Assert
        yaml.ShouldNotBe(SopsSamples.Yaml(Both));
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("tags", StringComparison.Ordinal);
    }

    [Fact]
    public void A_tag_directive_before_a_document_is_refused()
    {
        // Act
        var result = SopsMetadataReader.Read("%TAG !x! tag:yaml.org,2002:bi\n---\n" + SopsSamples.Yaml(Both), SopsFormat.Yaml);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("tags", StringComparison.Ordinal);
    }

    [Fact]
    public void An_exclamation_mark_inside_a_word_is_not_a_tag()
    {
        // Act
        var holders = SopsMetadataReader.Read(SopsSamples.Yaml(Both, dataLines: "greeting: hello!\n"), SopsFormat.Yaml).ShouldBeSuccess();

        // Assert
        holders.Concerns.ShouldHaveSingleItem().ShouldBe("1 value is not encrypted");
    }

    [Fact]
    public void A_JSON_byte_order_mark_or_second_document_is_refused()
    {
        // Act
        var bom = SopsMetadataReader.Read("\uFEFF" + SopsSamples.Json(Both), SopsFormat.Json);
        var two = SopsMetadataReader.Read(SopsSamples.Json(Both) + SopsSamples.Json([TestRecipients.First]), SopsFormat.Json);

        // Assert
        bom.IsFailure.ShouldBeTrue();
        two.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void The_same_recipient_listed_twice_is_an_error()
    {
        // Act
        var yaml = SopsMetadataReader.Read(SopsSamples.Yaml([TestRecipients.First, TestRecipients.Second]).Replace(TestRecipients.Second, TestRecipients.First, StringComparison.Ordinal), SopsFormat.Yaml);

        // Assert
        yaml.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void A_label_cannot_be_given_to_a_different_envelope_type()
    {
        // Arrange
        var ssh = $"{{\"sops\":{{\"age\":[{{\"recipient\":\"{TestRecipients.First}\",\"enc\":{AgeEnvelopes.JsonLiteral(TestRecipients.SshEd25519)}}}]}}}}";

        // Act
        var result = SopsMetadataReader.Read(ssh, SopsFormat.Json);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void An_ssh_label_must_match_the_tag_in_its_envelope()
    {
        // Arrange
        var blob = Convert.FromBase64String(TestRecipients.SshEd25519.Split(' ')[1]);
        blob[^1] ^= 1;
        var otherSshKey = "ssh-ed25519 " + Convert.ToBase64String(blob);
        var forged = $"{{\"sops\":{{\"age\":[{{\"recipient\":\"{TestRecipients.SshEd25519}\",\"enc\":{AgeEnvelopes.JsonLiteral(otherSshKey)}}}]}}}}";
        var honest = $"{{\"sops\":{{\"age\":[{{\"recipient\":\"{TestRecipients.SshEd25519}\",\"enc\":{AgeEnvelopes.JsonLiteral(TestRecipients.SshEd25519)}}}]}}}}";

        // Act
        var forgedResult = SopsMetadataReader.Read(forged, SopsFormat.Json);
        var honestResult = SopsMetadataReader.Read(honest, SopsFormat.Json);

        // Assert
        forgedResult.IsFailure.ShouldBeTrue();
        honestResult.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("none")]
    [InlineData("two")]
    [InlineData("grease")]
    [InlineData("missing")]
    [InlineData("notarmor")]
    [InlineData("badbase64")]
    [InlineData("wrongmagic")]
    [InlineData("shortshare")]
    [InlineData("empty")]
    [InlineData("wrongtype")]
    public void An_envelope_that_is_not_exactly_one_recipient_stanza_is_an_error(string kind)
    {
        // Arrange
        var share = AgeEnvelopes.Base64Unpadded(new byte[32]);
        var enc = kind switch
        {
            "none" => AgeEnvelopes.Armored(),
            "two" => AgeEnvelopes.Armored("X25519 " + share, "X25519 " + share),
            "grease" => AgeEnvelopes.Armored("X25519 " + share, "grease-stanza abc"),
            "notarmor" => "age-encryption.org/v1",
            "badbase64" => "-----BEGIN AGE ENCRYPTED FILE-----\n!!!!\n-----END AGE ENCRYPTED FILE-----\n",
            "wrongmagic" => AgeEnvelopes.Armored("X25519 " + share).Replace("-----BEGIN AGE ENCRYPTED FILE-----\n", "-----BEGIN AGE ENCRYPTED FILE-----\nAAAA", StringComparison.Ordinal),
            "shortshare" => AgeEnvelopes.Armored("X25519 " + share[..20]),
            "empty" => string.Empty,
            "wrongtype" => AgeEnvelopes.Armored("ssh-rsa " + share),
            _ => null,
        };
        var json = kind == "missing"
            ? $"{{\"sops\":{{\"age\":[{{\"recipient\":\"{TestRecipients.First}\"}}]}}}}"
            : $"{{\"sops\":{{\"age\":[{{\"recipient\":\"{TestRecipients.First}\",\"enc\":{System.Text.Json.JsonSerializer.Serialize(enc)}}}]}}}}";

        // Act
        var result = SopsMetadataReader.Read(json, SopsFormat.Json);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Two_entries_that_share_one_envelope_are_an_error()
    {
        // Arrange
        var enc = AgeEnvelopes.JsonLiteral(TestRecipients.First);
        var json = $"{{\"sops\":{{\"age\":[{{\"recipient\":\"{TestRecipients.First}\",\"enc\":{enc}}},{{\"recipient\":\"{TestRecipients.Second}\",\"enc\":{enc}}}]}}}}";

        // Act
        var result = SopsMetadataReader.Read(json, SopsFormat.Json);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("unencrypted_regex: .*")]
    [InlineData("encrypted_regex: ^x$")]
    [InlineData("unencrypted_comment_regex: .*")]
    [InlineData("encrypted_comment_regex: .*")]
    [InlineData("encrypted_suffix: _secret")]
    [InlineData("mac_only_encrypted: true")]
    public void A_non_default_encryption_setting_in_yaml_is_a_concern_and_the_file_is_not_exact(string setting)
    {
        ArgumentNullException.ThrowIfNull(setting);
        // Arrange
        var yaml = SopsSamples.Yaml(Both, "    " + setting + "\n");

        // Act
        var holders = SopsMetadataReader.Read(yaml, SopsFormat.Yaml).ShouldBeSuccess();
        var verification = RecipientVerification.Compare(holders, TestRecipients.Rule("x", 2, TestRecipients.First, TestRecipients.Second));

        // Assert
        holders.Concerns.ShouldHaveSingleItem().ShouldContain(setting[..setting.IndexOf(':', StringComparison.Ordinal)], StringComparison.Ordinal);
        verification.IsExact.ShouldBeFalse();
    }

    [Fact]
    public void A_non_default_unencrypted_suffix_and_JSON_settings_are_concerns()
    {
        // Act
        var suffix = SopsMetadataReader.Read(SopsSamples.Yaml(Both).Replace("_unencrypted", "_clear", StringComparison.Ordinal), SopsFormat.Yaml).ShouldBeSuccess();
        var regex = SopsMetadataReader.Read(SopsSamples.Json(Both, ",\"unencrypted_regex\":\".*\""), SopsFormat.Json).ShouldBeSuccess();
        var macOnly = SopsMetadataReader.Read(SopsSamples.Json(Both, ",\"mac_only_encrypted\":true"), SopsFormat.Json).ShouldBeSuccess();
        var macOnlyOff = SopsMetadataReader.Read(SopsSamples.Json(Both, ",\"mac_only_encrypted\":false"), SopsFormat.Json).ShouldBeSuccess();

        // Assert
        suffix.Concerns.ShouldHaveSingleItem();
        regex.Concerns.ShouldHaveSingleItem();
        macOnly.Concerns.ShouldHaveSingleItem();
        macOnlyOff.Concerns.ShouldBeEmpty();
    }

    [Fact]
    public void A_plaintext_value_in_yaml_or_JSON_is_a_concern()
    {
        // Act
        var yaml = SopsMetadataReader.Read(SopsSamples.Yaml(Both, dataLines: "token: hunter2\n"), SopsFormat.Yaml).ShouldBeSuccess();
        var json = SopsMetadataReader.Read(SopsSamples.Json(Both, dataMembers: ",\"token\":\"hunter2\",\"n\":1,\"b\":true"), SopsFormat.Json).ShouldBeSuccess();

        // Assert
        yaml.Concerns.ShouldHaveSingleItem().ShouldBe("1 value is not encrypted");
        json.Concerns.ShouldHaveSingleItem().ShouldBe("3 values are not encrypted");
    }

    [Theory]
    [InlineData("token: hunter2\n")]
    [InlineData("list:\n    - hunter2\n")]
    [InlineData("nested:\n    deep:\n        - key: plain\n")]
    [InlineData("multi: |\n    line one\n    line two\n")]
    [InlineData("quoted: \"hunter2\"\n")]
    [InlineData("number: 1\n")]
    [InlineData("flag: true\n")]
    public void Plaintext_in_any_yaml_shape_is_found(string data)
    {
        // Act
        var holders = SopsMetadataReader.Read(SopsSamples.Yaml(Both, dataLines: data), SopsFormat.Yaml).ShouldBeSuccess();

        // Assert
        holders.Concerns.ShouldHaveSingleItem();
    }

    [Fact]
    public void Containers_nulls_empty_strings_and_unencrypted_suffix_keys_are_not_plaintext_concerns()
    {
        // Arrange
        const string data = "a: null\nb: \"\"\ne: []\ng: {}\nh_unencrypted: plain\nnested_unencrypted:\n    x: plain\n    y:\n        - plain\nz: ~\n";

        // Act
        var yaml = SopsMetadataReader.Read(SopsSamples.Yaml(Both, dataLines: data), SopsFormat.Yaml).ShouldBeSuccess();
        var json = SopsMetadataReader.Read(SopsSamples.Json(Both, dataMembers: ",\"a\":null,\"b\":\"\",\"e\":[],\"g\":{},\"h_unencrypted\":\"plain\",\"n_unencrypted\":{\"x\":[1]}"), SopsFormat.Json).ShouldBeSuccess();

        // Assert
        yaml.Concerns.ShouldBeEmpty();
        json.Concerns.ShouldBeEmpty();
    }

    [Fact]
    public void The_hostile_new_file_shape_is_not_exact_for_any_rule()
    {
        // Arrange
        var yaml = $"password: hunter2\n{SopsSamples.FakeBlock(TestRecipients.First)}    unencrypted_regex: .*\n";

        // Act
        var holders = SopsMetadataReader.Read(yaml, SopsFormat.Yaml).ShouldBeSuccess();

        // Assert
        RecipientVerification.Compare(holders, AllowedOnly).IsExact.ShouldBeFalse();
        holders.Concerns.Count.ShouldBe(2);
    }

    [Fact]
    public void An_envelope_larger_than_the_limit_is_an_error()
    {
        // Arrange
        var huge = "-----BEGIN AGE ENCRYPTED FILE-----\n" + new string('A', 200_000) + "\n-----END AGE ENCRYPTED FILE-----\n";
        var json = $"{{\"sops\":{{\"age\":[{{\"recipient\":\"{TestRecipients.First}\",\"enc\":\"{huge.Replace("\n", "\\n", StringComparison.Ordinal)}\"}}]}}}}";

        // Act
        var result = SopsMetadataReader.Read(json, SopsFormat.Json);

        // Assert
        result.IsFailure.ShouldBeTrue();
        Encoding.UTF8.GetByteCount(json).ShouldBeGreaterThan(200_000);
    }
}
