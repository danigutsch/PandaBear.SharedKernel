namespace PandaBear.Secrets.Tests;

/// <summary>Tests the safe relative path of an encrypted file.</summary>
[Trait("Capability", "Secrets")]
public sealed class EncryptedFilePathTests
{
    [Theory]
    [InlineData("app.sops.yaml", SopsFormat.Yaml)]
    [InlineData("secrets/app.sops.yml", SopsFormat.Yaml)]
    [InlineData("a/b/c/db.sops.json", SopsFormat.Json)]
    public void A_sops_named_relative_path_is_kept_with_its_format(string input, SopsFormat format)
    {
        // Act
        var path = EncryptedFilePath.Create(input).ShouldBeSuccess();

        // Assert
        path.Value.ShouldBe(input);
        path.Format.ShouldBe(format);
        path.ToString().ShouldBe(input);
    }

    [Theory]
    [InlineData("app.sops.env")]
    [InlineData("app.sops.enc")]
    public void A_sops_name_with_another_extension_is_valid_but_has_no_readable_format(string input)
    {
        // Act
        var path = EncryptedFilePath.Create(input).ShouldBeSuccess();

        // Assert
        path.Format.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/etc/app.sops.yaml")]
    [InlineData("../app.sops.yaml")]
    [InlineData("a/../app.sops.yaml")]
    [InlineData("a/./app.sops.yaml")]
    [InlineData("a//app.sops.yaml")]
    [InlineData("a/app.sops.yaml/")]
    [InlineData("a\\app.sops.yaml")]
    [InlineData("C:app.sops.yaml")]
    [InlineData("app.sops.yaml\n")]
    [InlineData("app.yaml")]
    [InlineData(".sops.yaml")]
    [InlineData("app.sops.")]
    [InlineData("sops.yaml")]
    public void An_unsafe_or_misnamed_path_is_rejected_without_echoing_it(string? input)
    {
        // Act
        var result = EncryptedFilePath.Create(input);

        // Assert
        var message = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["file"].ShouldHaveSingleItem();
        if (!string.IsNullOrWhiteSpace(input))
        {
            message.ShouldNotContain(input, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_path_longer_than_the_limit_is_rejected()
    {
        // Act
        var result = EncryptedFilePath.Create(new string('a', EncryptedFilePath.MaxLength) + ".sops.yaml");

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void ResolveUnder_joins_the_root_and_the_path()
    {
        // Arrange
        using var root = new TemporaryDirectory();

        // Act
        var resolved = TestRecipients.File("secrets/app.sops.yaml").ResolveUnder(root.Path).ShouldBeSuccess();

        // Assert
        resolved.ShouldBe(Path.Combine(root.Path, "secrets", "app.sops.yaml"));
    }

    [Fact]
    public void ResolveUnder_refuses_a_symbolic_link_in_any_segment()
    {
        // Arrange
        using var root = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        Directory.CreateSymbolicLink(Path.Combine(root.Path, "linked"), outside.Path);

        // Act
        var result = TestRecipients.File("linked/app.sops.yaml").ResolveUnder(root.Path);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void ResolveUnder_refuses_a_relative_root()
    {
        // Act
        var result = TestRecipients.File("app.sops.yaml").ResolveUnder("relative");

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void ResolveUnder_rejects_null()
    {
        // Assert
        Action act1 = () => TestRecipients.File("app.sops.yaml").ResolveUnder(null!);
        act1.ShouldThrow<ArgumentNullException>();
    }
}
