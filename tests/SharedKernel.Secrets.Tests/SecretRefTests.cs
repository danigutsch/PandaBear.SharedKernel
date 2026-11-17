namespace PandaBear.Secrets.Tests;

/// <summary>Tests references to a value inside an encrypted file.</summary>
[Trait("Capability", "Secrets")]
public sealed class SecretRefTests
{
    [Theory]
    [InlineData("secrets/app.sops.yaml#db.password", "db.password", "[\"db\"][\"password\"]")]
    [InlineData("app.sops.json#token", "token", "[\"token\"]")]
    [InlineData("app.sops.yaml#servers[0].token", "servers[0].token", "[\"servers\"][0][\"token\"]")]
    [InlineData("app.sops.yaml#grid[1][22]", "grid[1][22]", "[\"grid\"][1][22]")]
    [InlineData("app.sops.yaml#a_b-c.D9", "a_b-c.D9", "[\"a_b-c\"][\"D9\"]")]
    public void A_valid_reference_gives_the_file_the_key_path_and_the_sops_extract_path(string input, string keyPath, string extract)
    {
        // Act
        var reference = SecretRef.Create(input).ShouldBeSuccess();

        // Assert
        reference.File.Value.ShouldBe(string.Concat(input.TakeWhile(static c => c != '#')));
        reference.KeyPath.ShouldBe(keyPath);
        reference.SopsExtractPath.ShouldBe(extract);
        reference.ToString().ShouldBe(input);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("app.sops.yaml")]
    [InlineData("app.sops.yaml#")]
    [InlineData("#a")]
    [InlineData("app.sops.yaml#a#b")]
    [InlineData("app.yaml#a")]
    [InlineData("../app.sops.yaml#a")]
    [InlineData("app.sops.yaml#a..b")]
    [InlineData("app.sops.yaml#.a")]
    [InlineData("app.sops.yaml#a b")]
    [InlineData("app.sops.yaml#a\"]; rm")]
    [InlineData("app.sops.yaml#a[x]")]
    [InlineData("app.sops.yaml#a[]")]
    [InlineData("app.sops.yaml#a[1")]
    [InlineData("app.sops.yaml#a[1234567]")]
    [InlineData("app.sops.yaml#[0]")]
    [InlineData("app.sops.yaml#a[0]b")]
    public void An_invalid_reference_is_rejected_without_echoing_it(string? input)
    {
        // Act
        var result = SecretRef.Create(input);

        // Assert
        var message = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["secret"].ShouldHaveSingleItem();
        if (!string.IsNullOrEmpty(input))
        {
            message.ShouldNotContain(input, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_key_path_with_too_many_segments_is_rejected()
    {
        // Act
        var result = SecretRef.Create("app.sops.yaml#" + string.Join('.', Enumerable.Repeat("a", 33)));

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void References_with_the_same_file_and_key_path_are_equal()
    {
        // Assert
        SecretRef.Create("a.sops.yaml#x.y").ShouldBeSuccess().ShouldBe(SecretRef.Create("a.sops.yaml#x.y").ShouldBeSuccess());
        SecretRef.Create("a.sops.yaml#x.y").ShouldBeSuccess().ShouldNotBe(SecretRef.Create("a.sops.yaml#x.z").ShouldBeSuccess());
    }
}
