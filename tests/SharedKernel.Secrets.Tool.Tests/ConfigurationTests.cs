using static SharedKernel.Secrets.Tool.Tests.SecretsToolHarness;

namespace SharedKernel.Secrets.Tool.Tests;

/// <summary>Tests the strict JSON configuration, through <c>verify</c>, which reads it first.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class ConfigurationTests
{
    [Fact]
    public async Task A_missing_file_is_a_failure_that_names_it()
    {
        // Arrange
        using var repository = CreateRepository(null);

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain(".config/sharedkernel-secrets.json does not exist.", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"version\":1}")]
    [InlineData("{\"version\":1,\"creationRules\":[],\"extra\":1}")]
    [InlineData("{\"version\":1,\"creationRules\":[],\"creationRules\":[]}")]
    [InlineData("{\"version\":1,\"creationRules\":[],}")]
    [InlineData("{\"version\":1,\"creationRules\":[null]}")]
    [InlineData("{\"version\":1,\"creationRules\":[{\"pathRegex\":\"x\",\"recipients\":[\"age1\"]}]}")]
    [InlineData("// comment\n{\"version\":1,\"creationRules\":[]}")]
    public async Task A_malformed_configuration_is_a_failure_with_a_location_and_no_value(string configuration)
    {
        // Arrange
        using var repository = CreateRepository(configuration);

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain(".config/sharedkernel-secrets.json", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Version_other_than_one_is_refused()
    {
        // Arrange
        using var repository = CreateRepository("{\"version\":2,\"creationRules\":[]}");

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("$.version must be 1.", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"age1notarecipient\"", 1, "x", "$.creationRules[0].recipients[0]")]
    [InlineData("\"" + TestRecipients.First + "\",\"" + TestRecipients.First + "\"", 1, "x", "$.creationRules[0].recipients")]
    [InlineData("\"" + TestRecipients.First + "\"", 2, "x", "$.creationRules[0].recipients")]
    [InlineData("\"" + TestRecipients.First + "\"", 0, "x", "$.creationRules[0].minimumRecipients")]
    [InlineData("\"" + TestRecipients.First + "\"", 1, "(?=x)", "$.creationRules[0].pathRegex")]
    public async Task An_invalid_rule_is_a_failure_at_its_JSON_path_without_echoing_the_value(string recipients, int minimum, string pattern, string location)
    {
        // Arrange
        using var repository = CreateRepository($"{{\"version\":1,\"creationRules\":[{{\"pathRegex\":\"{pattern}\",\"recipients\":[{recipients}],\"minimumRecipients\":{minimum}}}]}}");

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain(location, StringComparison.Ordinal);
        run.Error.ShouldNotContain("age1notarecipient", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_configuration_that_is_a_symbolic_link_is_not_followed()
    {
        // Arrange
        using var repository = CreateRepository(null);
        using var elsewhere = CreateRepository(TestRecipients.Config(1, TestRecipients.First));
        Directory.CreateSymbolicLink(Path.Combine(repository.Path, ".config"), Path.Combine(elsewhere.Path, ".config"));

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("Symbolic links are not followed", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_configuration_larger_than_one_mebibyte_is_refused()
    {
        // Arrange
        using var repository = CreateRepository("{\"version\":1,\"creationRules\":[],\"x\":\"" + new string('a', 1024 * 1024) + "\"}");

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("larger than 1 MiB", StringComparison.Ordinal);
    }
}
