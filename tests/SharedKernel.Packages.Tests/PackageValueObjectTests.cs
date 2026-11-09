namespace PandaBear.Packages.Tests;

/// <summary>Tests the validated package name and version.</summary>
[Trait("Capability", "Packages")]
public sealed class PackageValueObjectTests
{
    [Theory]
    [InlineData("git")]
    [InlineData("libstdc++6")]
    [InlineData("libc6:amd64")]
    [InlineData("@anthropic-ai/claude-code")]
    [InlineData("ripgrep_all")]
    [InlineData("7zip")]
    [InlineData("0ad")]
    public void A_valid_name_is_kept_exactly(string input)
    {
        // Act
        var name = PackageName.Create(input).ShouldBeSuccess();

        // Assert
        name.Value.ShouldBe(input);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" git")]
    [InlineData("-git")]
    [InlineData("has space")]
    [InlineData("tab\there")]
    [InlineData("café")]
    public void An_invalid_name_is_rejected_without_echoing_it(string? input)
    {
        // Act
        var result = PackageName.Create(input);

        // Assert
        var errors = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        errors["name"].ShouldHaveSingleItem();
        if (!string.IsNullOrEmpty(input))
        {
            errors["name"][0].ShouldNotContain(input, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_name_longer_than_the_limit_is_rejected()
    {
        // Act
        var result = PackageName.Create(new string('a', PackageName.MaxLength + 1));

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["name"].ShouldBe(["Package name cannot exceed 214 characters."]);
    }

    [Fact]
    public void Names_that_differ_only_in_case_are_different()
    {
        // Act
        var lower = PackageName.Create("git").Value;
        var upper = PackageName.Create("Git").Value;

        // Assert
        lower.ShouldNotBe(upper);
    }

    [Fact]
    public void Equal_names_collapse_in_a_set()
    {
        // Act
        HashSet<PackageName> set = [PackageName.Create("git").Value, PackageName.Create("git").Value];

        // Assert
        set.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("1:2.43.0-1ubuntu7")]
    [InlineData("0.1.0-alpha.3+build.5")]
    [InlineData("24.18.1")]
    public void A_valid_version_is_kept_exactly(string input)
    {
        // Act
        var version = PackageVersion.Create(input).ShouldBeSuccess();

        // Assert
        version.Value.ShouldBe(input);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.0 beta")]
    [InlineData("1.0\n")]
    [InlineData("vé")]
    public void An_invalid_version_is_rejected(string? input)
    {
        // Act
        var result = PackageVersion.Create(input);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["version"].ShouldHaveSingleItem();
    }

    [Fact]
    public void A_name_and_a_version_of_exactly_the_longest_allowed_length_are_accepted()
    {
        // Act
        var name = PackageName.Create(new string('a', PackageName.MaxLength));
        var version = PackageVersion.Create(new string('1', PackageVersion.MaxLength));

        // Assert
        name.IsSuccess.ShouldBeTrue();
        version.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("1\u007f2")]
    [InlineData("1\t2")]
    public void A_version_with_a_delete_or_control_character_is_rejected_without_echoing_it(string input)
    {
        // Act
        var result = PackageVersion.Create(input);

        // Assert
        var message = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["version"].ShouldHaveSingleItem();
        message.ShouldNotContain(input, StringComparison.Ordinal);
    }
}
