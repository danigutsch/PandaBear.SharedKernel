using System.Text;

namespace PandaBear.Secrets.Tests;

/// <summary>Tests the conservative rule: a file is NotSops only when it positively cannot be SOPS.</summary>
[Trait("Capability", "Secrets")]
public sealed class SopsSnifferTests
{
    [Theory]
    [InlineData("")]
    [InlineData("hello world\n")]
    [InlineData("{}")]
    [InlineData("{\"a\": 1, \"b\": [1, 2]}")]
    [InlineData("{\"data\": ")]
    [InlineData("tools:\n  name: x\n")]
    [InlineData("# just a comment\nkey: value\n")]
    [InlineData("\uFEFFplain text")]
    public void Text_with_no_sops_token_and_no_encrypted_value_is_positively_not_sops(string text)
    {
        // Act
        var result = SopsSniffer.Classify(Encoding.UTF8.GetBytes(text), out _, out _);

        // Assert
        result.ShouldBe(SniffResult.NotSops);
    }

    [Theory]
    [InlineData("a sops mention")]
    [InlineData("SOPS")]
    [InlineData("SoPs")]
    [InlineData("s\\x6fps: x")]
    [InlineData("\"\\u0073ops\": x")]
    [InlineData("\"\\U00000073ops\": x")]
    [InlineData("\u017fops: x")]
    [InlineData("\uff53\uff4f\uff50\uff53: x")]
    [InlineData("so\u200bps: x")]
    [InlineData("\"so\\\n   ps\": x")]
    [InlineData("password: ENC[AES256_GCM,data:x]")]
    [InlineData("enc[x]")]
    [InlineData("\uff25\uff2e\uff23\uff3b")]
    [InlineData("\"\\x45NC[\"")]
    public void A_sops_token_or_an_encrypted_value_in_any_spelling_prevents_calling_a_file_not_sops(string text)
    {
        // Act
        var result = SopsSniffer.Classify(Encoding.UTF8.GetBytes(text), out _, out _);

        // Assert
        result.ShouldNotBe(SniffResult.NotSops);
    }

    [Theory]
    [InlineData("\"so\\\u2028ps\": x")]
    [InlineData("\"so\\\u2029ps\": x")]
    [InlineData("\"so\\\u0085ps\": x")]
    [InlineData("\"so\\\nps\": x")]
    [InlineData("\"so\\\r\nps\": x")]
    [InlineData("\"so\\\u2028   ps\": x")]
    [InlineData("\"E\\\u2028NC[AES256_GCM]\"")]
    [InlineData("\"E\\\u2029NC[AES256_GCM]\"")]
    [InlineData("\"E\\\u0085NC[AES256_GCM]\"")]
    public void A_backslash_before_any_line_break_character_is_a_line_continuation_that_reads_as_nothing(string text)
    {
        // Act
        var result = SopsSniffer.Classify(Encoding.UTF8.GetBytes(text), out _, out _);

        // Assert
        result.ShouldNotBe(SniffResult.NotSops);
    }

    [Theory]
    [InlineData("s o p s")]
    [InlineData("s.o.p.s")]
    [InlineData("s\\ o\\ p\\ s")]
    [InlineData("s\u200bo\u00a0p\u2003s")]
    [InlineData("S\u3000O\u2000P\u200as")]
    [InlineData("s\u2001o\u2002p\u2003s\u2004:")]
    [InlineData("s-o_p/s")]
    [InlineData("e n c [")]
    [InlineData("E\u00a0N\u2003C \u3000[")]
    [InlineData("e.n.c.[x]")]
    [InlineData("e n [ c [")]
    [InlineData("sop s")]
    public void Letters_of_the_tokens_separated_by_anything_that_is_not_a_letter_or_digit_still_count(string text)
    {
        // Act
        var result = SopsSniffer.Classify(Encoding.UTF8.GetBytes(text), out _, out _);

        // Assert
        result.ShouldNotBe(SniffResult.NotSops);
    }

    [Theory]
    [InlineData("soap and ops")]
    [InlineData("enc (x)")]
    [InlineData("ENC(x)")]
    [InlineData("s1o2p3s")]
    [InlineData("sosps")]
    [InlineData("e n x [")]
    [InlineData("ncc[")]
    [InlineData("enc text [x]")]
    [InlineData("enc, then words, then [brackets]")]
    public void Text_whose_letters_do_not_spell_a_token_even_when_separators_are_ignored_is_not_sops(string text)
    {
        // Act
        var result = SopsSniffer.Classify(Encoding.UTF8.GetBytes(text), out _, out _);

        // Assert
        result.ShouldBe(SniffResult.NotSops);
    }

    [Theory]
    [InlineData("? sops\n:\n")]
    [InlineData("!!str sops:\n  a: b\n")]
    [InlineData("&x sops:\n  a: b\n")]
    [InlineData("sops:\t\n  a: b\n")]
    [InlineData("# note\n{\"data\":1,\"sops\":{}}")]
    [InlineData("---\n{\"data\":1,\"sops\":{}}")]
    [InlineData("; comment\n  [sops]\nage = x\n")]
    [InlineData("sops_age__list_0__map_recipient=age1x\n")]
    public void The_shapes_of_the_fourth_review_are_never_not_sops(string text)
    {
        // Act
        var result = SopsSniffer.Classify(Encoding.UTF8.GetBytes(text), out _, out _);

        // Assert
        result.ShouldNotBe(SniffResult.NotSops);
    }

    [Fact]
    public void Binary_and_non_utf8_content_cannot_be_ruled_out()
    {
        // Act
        var binary = SopsSniffer.Classify([0xff, 0xfe, 0x00], out _, out _);
        var nul = SopsSniffer.Classify([(byte)'a', 0, (byte)'b'], out _, out _);

        // Assert
        binary.ShouldBe(SniffResult.Unknown);
        nul.ShouldBe(SniffResult.Unknown);
    }

    [Fact]
    public void A_JSON_document_and_a_yaml_key_that_are_positively_sops_are_recognised_with_their_format()
    {
        // Act
        var json = SopsSniffer.Classify(Encoding.UTF8.GetBytes(SopsSamples.Json([TestRecipients.First])), out var jsonFormat, out _);
        var yaml = SopsSniffer.Classify(Encoding.UTF8.GetBytes(SopsSamples.Yaml([TestRecipients.First])), out var yamlFormat, out _);

        // Assert
        json.ShouldBe(SniffResult.Sops);
        jsonFormat.ShouldBe(SopsFormat.Json);
        yaml.ShouldBe(SniffResult.Sops);
        yamlFormat.ShouldBe(SopsFormat.Yaml);
    }

    [Fact]
    public void Unknown_says_why()
    {
        // Act
        var result = SopsSniffer.Classify(Encoding.UTF8.GetBytes("a sops mention"), out _, out var reason);

        // Assert
        result.ShouldBe(SniffResult.Unknown);
        reason.ShouldNotBeEmpty();
    }
}
