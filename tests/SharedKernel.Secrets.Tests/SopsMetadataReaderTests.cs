namespace PandaBear.Secrets.Tests;

/// <summary>Tests reading the key holders from SOPS metadata, including files it must refuse.</summary>
[Trait("Capability", "Secrets")]
public sealed class SopsMetadataReaderTests
{
    private static readonly string[] Both = [TestRecipients.First, TestRecipients.Second];

    [Fact]
    public void YAML_age_recipients_are_read_and_ciphertext_blocks_are_ignored()
    {
        // Act
        var holders = SopsMetadataReader.Read(SopsSamples.Yaml(Both), SopsFormat.Yaml).ShouldBeSuccess();

        // Assert
        holders.Age.Select(static r => r.Value).ShouldBe([TestRecipients.Second, TestRecipients.First]);
        holders.OtherKeyTypes.ShouldBeEmpty();
    }

    [Fact]
    public void JSON_age_recipients_are_read()
    {
        // Act
        var holders = SopsMetadataReader.Read(SopsSamples.Json(Both), SopsFormat.Json).ShouldBeSuccess();

        // Assert
        holders.Age.Select(static r => r.Value).ShouldBe([TestRecipients.Second, TestRecipients.First]);
        holders.OtherKeyTypes.ShouldBeEmpty();
    }

    [Fact]
    public void YAML_with_windows_line_endings_and_quoted_recipients_is_read()
    {
        // Arrange
        var yaml = SopsSamples.Yaml([TestRecipients.First]).Replace($"recipient: {TestRecipients.First}", $"recipient: \"{TestRecipients.First}\"", StringComparison.Ordinal).ReplaceLineEndings("\r\n");

        // Act
        var holders = SopsMetadataReader.Read(yaml, SopsFormat.Yaml).ShouldBeSuccess();

        // Assert
        holders.Age.Select(static r => r.Value).ShouldBe([TestRecipients.First]);
    }

    [Fact]
    public void A_kms_or_pgp_key_holder_is_reported_by_name()
    {
        // Arrange
        var yaml = SopsSamples.Yaml(Both).Replace("    kms: []\n", "    kms:\n        - arn: arn:aws:kms:eu-west-1:111122223333:key/abc\n          created: 2026-01-01T00:00:00Z\n          enc: AQIDAH\n", StringComparison.Ordinal);
        var json = SopsSamples.Json(Both, ",\"hc_vault\":[{\"vault_address\":\"https://v\"}]");

        // Act
        var fromYaml = SopsMetadataReader.Read(yaml, SopsFormat.Yaml).ShouldBeSuccess();
        var fromJson = SopsMetadataReader.Read(json, SopsFormat.Json).ShouldBeSuccess();

        // Assert
        fromYaml.OtherKeyTypes.ShouldBe(["kms"]);
        fromJson.OtherKeyTypes.ShouldBe(["hc_vault"]);
    }

    [Fact]
    public void Age_recipients_in_key_groups_are_collected_and_other_key_types_in_a_group_are_reported()
    {
        // Arrange
        var json = $"{{\"sops\":{{\"key_groups\":[{{\"age\":[{{\"recipient\":\"{TestRecipients.First}\",\"enc\":{AgeEnvelopes.JsonLiteral(TestRecipients.First)}}}]}},{{\"age\":[{{\"recipient\":\"{TestRecipients.Second}\",\"enc\":{AgeEnvelopes.JsonLiteral(TestRecipients.Second)}}}],\"pgp\":[{{\"fp\":\"ABC\"}}]}}],\"version\":\"3.13.0\"}}}}";

        // Act
        var holders = SopsMetadataReader.Read(json, SopsFormat.Json).ShouldBeSuccess();

        // Assert
        holders.Age.Select(static r => r.Value).ShouldBe([TestRecipients.Second, TestRecipients.First]);
        holders.OtherKeyTypes.ShouldBe(["pgp"]);
    }

    [Fact]
    public void A_duplicate_age_entry_is_an_error_not_silently_collapsed()
    {
        // Act
        var result = SopsMetadataReader.Read(SopsSamples.Json([TestRecipients.First, TestRecipients.First]), SopsFormat.Json);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void A_file_with_no_age_recipients_yields_an_empty_list_not_an_error()
    {
        // Act
        var holders = SopsMetadataReader.Read("{\"sops\":{\"version\":\"3.13.0\"}}", SopsFormat.Json).ShouldBeSuccess();

        // Assert
        holders.Age.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("db:\n  a: b\n")]
    [InlineData("sops:\n")]
    [InlineData("sops: {age: []}\n")]
    [InlineData("sops:\n    age: [x]\n")]
    [InlineData("sops:\n    age:\n        - recipient: notarecipient\n")]
    [InlineData("sops:\n    age:\n        - enc: x\n")]
    [InlineData("sops:\n\tage: []\n")]
    [InlineData("sops:\n    age: &anchor\n        - recipient: age1\n")]
    [InlineData("sops:\n    age: []\n    age: []\n")]
    [InlineData("sops:\n    age: []\nsops:\n    age: []\n")]
    [InlineData("sops:\n    age:\n        - recipient: age1qqq\n         bad: indent\n")]
    [InlineData("sops:\n    key: \"unterminated\n")]
    [InlineData("sops:\n    key: \"esc\\aped\"\n")]
    [InlineData("sops:\n    key: !!tag value\n")]
    [InlineData("sops:\n    - age\n")]
    public void YAML_the_reader_does_not_fully_understand_is_an_error_never_an_empty_list(string yaml)
    {
        // Act
        var result = SopsMetadataReader.Read(yaml, SopsFormat.Yaml);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"sops\":[]}")]
    [InlineData("{\"sops\":{},\"sops\":{}}")]
    [InlineData("{\"sops\":{\"age\":{}}}")]
    [InlineData("{\"sops\":{\"age\":[{\"recipient\":5}]}}")]
    [InlineData("{\"sops\":{\"age\":[{\"recipient\":\"x\"}]}}")]
    [InlineData("{\"sops\":{\"age\":[],\"age\":[]}}")]
    [InlineData("{\"sops\":{\"key_groups\":{}}}")]
    [InlineData("{\"sops\":{\"key_groups\":[1]}}")]
    [InlineData("{\"sops\":{\"age\":[],}}")]
    [InlineData("{\"sops\":{/*c*/}}")]
    public void JSON_the_reader_does_not_fully_understand_is_an_error_never_an_empty_list(string json)
    {
        // Act
        var result = SopsMetadataReader.Read(json, SopsFormat.Json);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void An_error_never_echoes_file_content()
    {
        // Act
        var result = SopsMetadataReader.Read("sops:\n    age:\n        - recipient: SUPERSECRETMARKER\n", SopsFormat.Yaml);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldNotContain("SUPERSECRETMARKER", StringComparison.Ordinal);
    }

    [Fact]
    public void Read_rejects_null()
    {
        // Act
        Action act = () => SopsMetadataReader.Read(null!, SopsFormat.Yaml);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void YAML_sequence_items_at_the_same_indent_as_their_key_are_read()
    {
        // Arrange
        var yaml = $"sops:\n  age:\n  - recipient: {TestRecipients.First}\n    enc: |\n{AgeEnvelopes.Indented(TestRecipients.First, 6)}  - recipient: {TestRecipients.Second}\n    enc: |\n{AgeEnvelopes.Indented(TestRecipients.Second, 6)}  version: 3.13.0\n";

        // Act
        var holders = SopsMetadataReader.Read(yaml, SopsFormat.Yaml).ShouldBeSuccess();

        // Assert
        holders.Age.Count.ShouldBe(2);
    }

    [Fact]
    public void YAML_key_groups_with_nested_blocks_scalar_items_and_empty_flow_collections_are_read()
    {
        // Arrange
        var yaml = $"sops:\n    key_groups:\n        -\n            age:\n                - recipient: {TestRecipients.First}\n                  enc: |\n{AgeEnvelopes.Indented(TestRecipients.First, 20)}        - age:\n            - recipient: {TestRecipients.Second}\n              enc: |\n{AgeEnvelopes.Indented(TestRecipients.Second, 16)}    shamir_threshold: 1\n    empty: {{}}\n    list: []\n    scalars:\n        - one\n        - two\n";

        // Act
        var holders = SopsMetadataReader.Read(yaml, SopsFormat.Yaml).ShouldBeSuccess();

        // Assert
        holders.Age.Select(static r => r.Value).ShouldBe([TestRecipients.Second, TestRecipients.First]);
        holders.OtherKeyTypes.ShouldBe(["scalars"]);
    }

    [Fact]
    public void YAML_quoted_keys_are_read()
    {
        // Arrange
        var yaml = $"sops:\n    \"age\":\n        - recipient: {TestRecipients.First}\n          enc: |\n{AgeEnvelopes.Indented(TestRecipients.First, 12)}    'version': 3.13.0\n";

        // Act
        var holders = SopsMetadataReader.Read(yaml, SopsFormat.Yaml).ShouldBeSuccess();

        // Assert
        holders.Age.Select(static r => r.Value).ShouldBe([TestRecipients.First]);
    }

    [Theory]
    [InlineData("sops:\n    a:\n        b: [x\n")]
    [InlineData("sops:\n    \"a\"b: 1\n")]
    [InlineData("sops:\n    : 1\n")]
    [InlineData("sops:\n    a: 1\n  b: 2\n")]
    [InlineData("sops: &x\n    a: 1\n")]
    [InlineData("sops:\n    -\n")]
    [InlineData("sops:\n    \tage: []\n")]
    public void More_yaml_outside_the_subset_is_an_error(string yaml)
    {
        // Act
        var result = SopsMetadataReader.Read(yaml, SopsFormat.Yaml);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void YAML_nested_deeper_than_the_limit_is_an_error()
    {
        // Arrange
        var yaml = "sops:\n" + string.Concat(Enumerable.Range(1, 20).Select(static depth => new string(' ', depth * 2) + "k" + depth + ":\n")) + new string(' ', 42) + "leaf: 1\n";

        // Act
        var result = SopsMetadataReader.Read(yaml, SopsFormat.Yaml);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void YAML_with_comments_and_null_values_is_read()
    {
        // Arrange
        var yaml = $"# header\nsops: # metadata\n    # keys\n    kms: null\n    age:\n        - recipient: '{TestRecipients.First}' # admin\n          enc: |\n{AgeEnvelopes.Indented(TestRecipients.First, 12)}    version: 3.13.0 # sops\n";

        // Act
        var holders = SopsMetadataReader.Read(yaml, SopsFormat.Yaml).ShouldBeSuccess();

        // Assert
        holders.Age.Select(static r => r.Value).ShouldBe([TestRecipients.First]);
    }
}
