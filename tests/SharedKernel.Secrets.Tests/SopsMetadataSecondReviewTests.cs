namespace PandaBear.Secrets.Tests;

/// <summary>Tests the second security review: setting spellings, unknown properties, comments, and malformed ciphertext values.</summary>
[Trait("Capability", "Secrets")]
public sealed class SopsMetadataSecondReviewTests
{
    private static readonly string[] Both = [TestRecipients.First, TestRecipients.Second];

    [Theory]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("true")]
    [InlineData("yes")]
    [InlineData("Yes")]
    [InlineData("YES")]
    [InlineData("y")]
    [InlineData("Y")]
    [InlineData("on")]
    [InlineData("On")]
    [InlineData("ON")]
    [InlineData("\"true\"")]
    [InlineData("maybe")]
    public void Every_way_of_spelling_true_or_anything_unrecognised_for_mac_only_encrypted_is_a_concern(string spelling)
    {
        // Act
        var holders = SopsMetadataReader.Read(SopsSamples.Yaml(Both, "    mac_only_encrypted: " + spelling + "\n"), SopsFormat.Yaml).ShouldBeSuccess();

        // Assert
        holders.Concerns.ShouldHaveSingleItem().ShouldContain("mac_only_encrypted", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("no")]
    [InlineData("off")]
    [InlineData("n")]
    [InlineData("~")]
    public void The_false_spellings_are_not_a_concern(string spelling)
    {
        // Act
        var holders = SopsMetadataReader.Read(SopsSamples.Yaml(Both, "    mac_only_encrypted: " + spelling + "\n"), SopsFormat.Yaml).ShouldBeSuccess();

        // Assert
        holders.Concerns.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(",\"Mac_Only_Encrypted\":true", "mac_only_encrypted")]
    [InlineData(",\"MAC_ONLY_ENCRYPTED\":\"yes\"", "mac_only_encrypted")]
    [InlineData(",\"Unencrypted_Regex\":\".*\"", "unencrypted_regex")]
    [InlineData(",\"ENCRYPTED_SUFFIX\":\"_s\"", "encrypted_suffix")]
    [InlineData(",\"Unencrypted_Suffix\":\"_clear\"", "unencrypted_suffix")]
    public void JSON_setting_names_are_compared_without_regard_to_case_as_go_does(string member, string setting)
    {
        // Act
        var holders = SopsMetadataReader.Read(SopsSamples.Json(Both, member), SopsFormat.Json).ShouldBeSuccess();

        // Assert
        holders.Concerns.ShouldHaveSingleItem().ShouldContain(setting, StringComparison.Ordinal);
    }

    [Fact]
    public void Names_that_differ_only_in_case_are_a_repeated_property_and_an_error()
    {
        // Act
        var settings = SopsMetadataReader.Read(SopsSamples.Json(Both, ",\"mac_only_encrypted\":false,\"MAC_ONLY_ENCRYPTED\":true"), SopsFormat.Json);
        var topLevel = SopsMetadataReader.Read(SopsSamples.Json(Both).Insert(1, "\"SOPS\":{},"), SopsFormat.Json);
        var entry = $"{{\"sops\":{{\"age\":[{{\"recipient\":\"{TestRecipients.First}\",\"Recipient\":\"{TestRecipients.Second}\",\"enc\":{AgeEnvelopes.JsonLiteral(TestRecipients.First)}}}]}}}}";

        // Assert
        settings.IsFailure.ShouldBeTrue();
        topLevel.IsFailure.ShouldBeTrue();
        SopsMetadataReader.Read(entry, SopsFormat.Json).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void An_age_entry_with_differently_cased_field_names_is_read()
    {
        // Arrange
        var json = $"{{\"sops\":{{\"AGE\":[{{\"Recipient\":\"{TestRecipients.First}\",\"ENC\":{AgeEnvelopes.JsonLiteral(TestRecipients.First)}}}]}}}}";

        // Act
        var holders = SopsMetadataReader.Read(json, SopsFormat.Json).ShouldBeSuccess();

        // Assert
        holders.Age.Select(static r => r.Value).ShouldBe([TestRecipients.First]);
    }

    [Fact]
    public void An_unknown_property_in_the_sops_block_is_a_concern_whatever_its_value()
    {
        // Act
        var json = SopsMetadataReader.Read(SopsSamples.Json(Both, ",\"surprise\":\"x\""), SopsFormat.Json).ShouldBeSuccess();
        var yaml = SopsMetadataReader.Read(SopsSamples.Yaml(Both, "    surprise: x\n"), SopsFormat.Yaml).ShouldBeSuccess();

        // Assert
        json.Concerns.ShouldHaveSingleItem().ShouldBe("unknown property surprise");
        yaml.Concerns.ShouldHaveSingleItem().ShouldBe("unknown property surprise");
    }

    [Theory]
    [InlineData("password: ENC[X] # hunter2\n")]
    [InlineData("# hunter2\n")]
    [InlineData("#ENC[AES256_GCM,data:Zhnqwg==,iv:AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=,tag:AAECAwQFBgcICQoLDA0ODw==,type:comment]\n")]
    [InlineData("nested:\n    # note\n    k: ENC[X]\n")]
    public void A_comment_outside_the_sops_block_is_a_concern(string data)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(data);
        var lines = data.Replace("ENC[X]", SopsSamples.Encrypted, StringComparison.Ordinal);

        // Act
        var holders = SopsMetadataReader.Read(SopsSamples.Yaml(Both, dataLines: lines), SopsFormat.Yaml).ShouldBeSuccess();

        // Assert
        holders.Concerns.ShouldHaveSingleItem().ShouldBe("comment present");
    }

    [Fact]
    public void An_empty_comment_marker_and_a_hash_inside_a_quoted_value_are_not_comments()
    {
        // Act
        var holders = SopsMetadataReader.Read(SopsSamples.Yaml(Both, dataLines: "#\nk: ''\n"), SopsFormat.Yaml).ShouldBeSuccess();

        // Assert
        holders.Concerns.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("    # note\n    kms: []\n")]
    [InlineData("    kms: [] # note\n")]
    [InlineData("    version: 3.13.0 # note\n")]
    public void A_comment_inside_the_sops_block_is_refused(string lines)
    {
        // Arrange
        var yaml = $"db: {SopsSamples.Encrypted}\nsops:\n{lines}    age:\n        - recipient: {TestRecipients.First}\n          enc: |\n{AgeEnvelopes.Indented(TestRecipients.First, 12)}";

        // Act
        var result = SopsMetadataReader.Read(yaml, SopsFormat.Yaml);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void A_comment_after_the_sops_key_is_refused()
    {
        // Act
        var result = SopsMetadataReader.Read(SopsSamples.Yaml(Both).Replace("sops:\n", "sops: # note\n", StringComparison.Ordinal), SopsFormat.Yaml);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("ENC[hunter2]")]
    [InlineData("ENC[AES256_GCM,data:Zhnqwg==]")]
    [InlineData("ENC[AES256_GCM,data:!!,iv:AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=,tag:AAECAwQFBgcICQoLDA0ODw==,type:str]")]
    [InlineData("ENC[AES256_GCM,data:Zhnqwg==,iv:AAAA,tag:AAECAwQFBgcICQoLDA0ODw==,type:str]")]
    [InlineData("ENC[AES256_GCM,data:Zhnqwg==,iv:AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=,tag:AAAA,type:str]")]
    [InlineData("ENC[AES256_GCM,data:Zhnqwg==,iv:AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=,tag:AAECAwQFBgcICQoLDA0ODw==,type:secret]")]
    [InlineData("ENC[AES128_GCM,data:Zhnqwg==,iv:AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=,tag:AAECAwQFBgcICQoLDA0ODw==,type:str]")]
    [InlineData("ENC[AES256_GCM,iv:AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=,data:Zhnqwg==,tag:AAECAwQFBgcICQoLDA0ODw==,type:str]")]
    [InlineData("ENC[AES256_GCM,data:Zhnqwg==,iv:AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=,tag:AAECAwQFBgcICQoLDA0ODw==,type:str,extra:1]")]
    [InlineData("ENC[AES256_GCM,data:Zhnq wg==,iv:AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=,tag:AAECAwQFBgcICQoLDA0ODw==,type:str]")]
    public void A_value_that_only_looks_like_ciphertext_is_plaintext(string value)
    {
        // Act
        var yaml = SopsMetadataReader.Read(SopsSamples.Yaml(Both, dataLines: "token: " + value + "\n"), SopsFormat.Yaml).ShouldBeSuccess();
        var json = SopsMetadataReader.Read(SopsSamples.Json(Both, dataMembers: ",\"token\":\"" + value + "\""), SopsFormat.Json).ShouldBeSuccess();

        // Assert
        yaml.Concerns.ShouldHaveSingleItem().ShouldBe("1 value is not encrypted");
        json.Concerns.ShouldHaveSingleItem().ShouldBe("1 value is not encrypted");
    }

    [Theory]
    [InlineData("int")]
    [InlineData("float")]
    [InlineData("bool")]
    [InlineData("bytes")]
    [InlineData("comment")]
    public void Every_type_that_sops_writes_is_accepted(string type)
    {
        // Arrange
        var value = SopsSamples.Encrypted.Replace("type:str", "type:" + type, StringComparison.Ordinal);

        // Act
        var yaml = SopsMetadataReader.Read(SopsSamples.Yaml(Both, dataLines: "token: " + value + "\n"), SopsFormat.Yaml).ShouldBeSuccess();

        // Assert
        yaml.Concerns.ShouldBeEmpty();
    }

    [Fact]
    public void The_envelope_text_of_each_label_is_kept_so_files_can_be_compared_label_by_label()
    {
        // Act
        var holders = SopsMetadataReader.Read(SopsSamples.Json(Both), SopsFormat.Json).ShouldBeSuccess();

        // Assert
        holders.Envelopes.Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe([TestRecipients.Second, TestRecipients.First]);
        holders.Envelopes[TestRecipients.First].ShouldBe(AgeEnvelopes.For(TestRecipients.First).Trim());
    }
}
