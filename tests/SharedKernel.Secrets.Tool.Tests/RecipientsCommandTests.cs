using static PandaBear.Secrets.Tool.Tests.SecretsToolHarness;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Tests <c>recipients list</c> and <c>recipients check</c>, which start no process.</summary>
[Trait("Capability", "Secrets")]
public sealed class RecipientsCommandTests
{
    private static readonly string Config = TestRecipients.Config(2, TestRecipients.First, TestRecipients.Second);

    [Fact]
    public async Task List_prints_the_age_recipients_and_other_key_holders()
    {
        // Arrange
        using var repository = CreateRepository(null);
        WriteFile(repository.Path, "secrets/app.sops.json", TestRecipients.Json([TestRecipients.First, TestRecipients.Second], ",\"pgp\":[{\"fp\":\"ABC\"}]"));

        // Act
        var run = await Run(repository.Path, "recipients", "list", "secrets/app.sops.json", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain($"age: {TestRecipients.Second}\nage: {TestRecipients.First}\nother key holder: pgp", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_passes_for_the_exact_set_in_any_order()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/app.sops.yaml", TestRecipients.Yaml([TestRecipients.Second, TestRecipients.First]));

        // Act
        var run = await Run(repository.Path, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("secrets/app.sops.yaml: labels consistent", StringComparison.Ordinal);
        run.Output.ShouldContain("proves labels and structure, not who can decrypt", StringComparison.Ordinal);
        run.Output.ShouldNotContain(": ok", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_reports_a_subset_a_superset_and_a_non_age_holder_with_exit_code_one()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/subset.sops.yaml", TestRecipients.Yaml([TestRecipients.First]));
        WriteFile(repository.Path, "secrets/superset.sops.json", TestRecipients.Json([TestRecipients.First, TestRecipients.Second, TestRecipients.Third]));
        WriteFile(repository.Path, "secrets/kms.sops.json", TestRecipients.Json([TestRecipients.First, TestRecipients.Second], ",\"hc_vault\":[{\"vault_address\":\"https://v\"}]"));

        // Act
        var subset = await Run(repository.Path, "recipients", "check", "secrets/subset.sops.yaml", "--root", repository.Path);
        var superset = await Run(repository.Path, "recipients", "check", "secrets/superset.sops.json", "--root", repository.Path);
        var kms = await Run(repository.Path, "recipients", "check", "secrets/kms.sops.json", "--root", repository.Path);

        // Assert
        subset.ExitCode.ShouldBe(1);
        subset.Output.ShouldContain($"secrets/subset.sops.yaml: missing recipient {TestRecipients.Second}", StringComparison.Ordinal);
        superset.ExitCode.ShouldBe(1);
        superset.Output.ShouldContain($"secrets/superset.sops.json: unexpected recipient {TestRecipients.Third}", StringComparison.Ordinal);
        kms.ExitCode.ShouldBe(1);
        kms.Output.ShouldContain("secrets/kms.sops.json: non-age key holder hc_vault", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_reports_a_weakened_file_and_refuses_one_that_hides_its_metadata()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/weak.sops.yaml", "password: hunter2\n" + TestRecipients.Block([TestRecipients.First, TestRecipients.Second], "    unencrypted_regex: .*\n"));
        WriteFile(repository.Path, "secrets/hidden.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Third]) + "---\n" + TestRecipients.Block([TestRecipients.First, TestRecipients.Second]));

        // Act
        var weak = await Run(repository.Path, "recipients", "check", "secrets/weak.sops.yaml", "--root", repository.Path);
        var hidden = await Run(repository.Path, "recipients", "check", "secrets/hidden.sops.yaml", "--root", repository.Path);
        var list = await Run(repository.Path, "recipients", "list", "secrets/weak.sops.yaml", "--root", repository.Path);

        // Assert
        weak.ExitCode.ShouldBe(1);
        weak.Output.ShouldContain("secrets/weak.sops.yaml: weakened: non-default setting unencrypted_regex", StringComparison.Ordinal);
        hidden.ExitCode.ShouldBe(2);
        hidden.Output.ShouldBe(string.Empty);
        list.Output.ShouldContain("concern: 1 value is not encrypted", StringComparison.Ordinal);
    }


    [Fact]
    public async Task Check_fails_when_no_rule_matches_or_the_file_cannot_be_read()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "other/app.sops.yaml", TestRecipients.Yaml([TestRecipients.First]));
        WriteFile(repository.Path, "secrets/broken.sops.yaml", "sops: {age: []}\n");

        // Act
        var uncovered = await Run(repository.Path, "recipients", "check", "other/app.sops.yaml", "--root", repository.Path);
        var broken = await Run(repository.Path, "recipients", "check", "secrets/broken.sops.yaml", "--root", repository.Path);
        var missing = await Run(repository.Path, "recipients", "check", "secrets/missing.sops.yaml", "--root", repository.Path);

        // Assert
        uncovered.ExitCode.ShouldBe(2);
        uncovered.Error.ShouldContain("No creation rule matches this file.", StringComparison.Ordinal);
        broken.ExitCode.ShouldBe(2);
        broken.Error.ShouldContain("secrets/broken.sops.yaml:", StringComparison.Ordinal);
        missing.ExitCode.ShouldBe(2);
    }

    [Fact]
    public async Task Check_uses_the_first_matching_rule()
    {
        // Arrange
        var config = "{\"version\":1,\"creationRules\":["
            + $"{{\"pathRegex\":\"^secrets/prod/\",\"recipients\":[\"{TestRecipients.First}\",\"{TestRecipients.Second}\"],\"minimumRecipients\":2}},"
            + $"{{\"pathRegex\":\"^secrets/\",\"recipients\":[\"{TestRecipients.Third}\"],\"minimumRecipients\":1}}]}}";
        using var repository = CreateRepository(config);
        WriteFile(repository.Path, "secrets/prod/a.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));
        WriteFile(repository.Path, "secrets/dev.sops.yaml", TestRecipients.Yaml([TestRecipients.Third]));

        // Act
        var prod = await Run(repository.Path, "recipients", "check", "secrets/prod/a.sops.yaml", "--root", repository.Path);
        var dev = await Run(repository.Path, "recipients", "check", "secrets/dev.sops.yaml", "--root", repository.Path);

        // Assert
        prod.ExitCode.ShouldBe(0);
        dev.ExitCode.ShouldBe(0);
    }

    [Theory]
    [InlineData("secrets/app.yaml")]
    [InlineData("../app.sops.yaml")]
    public async Task An_invalid_path_is_a_failure(string file)
    {
        // Arrange
        using var repository = CreateRepository(Config);

        // Act
        var list = await Run(repository.Path, "recipients", "list", file, "--root", repository.Path);
        var check = await Run(repository.Path, "recipients", "check", file, "--root", repository.Path);

        // Assert
        list.ExitCode.ShouldBe(2);
        check.ExitCode.ShouldBe(2);
    }
}
