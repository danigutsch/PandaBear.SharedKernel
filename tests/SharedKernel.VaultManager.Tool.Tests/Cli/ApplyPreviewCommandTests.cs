using System.Text.Json.Nodes;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Tests.Pipeline;
using static PandaBear.VaultManager.Tool.Tests.Cli.PreviewInputs;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

public sealed class ApplyPreviewCommandTests : IDisposable
{
    private const string ExportPassword = "fixture preview export password";
    private const string Item = "bitwarden|vault.json|bw-github (\"GitHub\"): ";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-cli-apply-preview-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public async Task Dry_run_lists_the_changes_and_removals_and_writes_nothing()
    {
        var prompts = new TestPrompts(ExportPassword);
        var environment = prompts.Environment();
        (string vault, string catalog, string decisions) = await SharedCatalog(directory, environment, TestContext.Current.CancellationToken);
        await EditItem(catalog, item =>
        {
            item["name"] = "GitHub (work)";
            item["fields"]![0]!["value"] = "fixture-preview-new-value";
            item["notes"]!["text"] = "";
            JsonNode masked = item["uris"]![1]!.DeepClone();
            item["uris"] = new JsonArray(masked, new JsonObject { ["value"] = "https://github.com" });
        }, TestContext.Current.CancellationToken);
        string[] before = Directory.GetFiles(directory);

        var run = await CommandLineRun.Invoke(environment, ["apply", vault, "--catalog", catalog, "--decisions", decisions, "--dry-run"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        string[] lines = run.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.ShouldBe(
        [
            "Changes:",
            "  " + Item + "renamed to \"GitHub (work)\"",
            "  " + Item + "website address \"https://github.com/login\" removed",
            "  " + Item + "website address \"https://github.com\" added",
            "  " + Item + "masked website address 1 moved",
            "  " + Item + "text field \"Plan\" changed",
            "  " + Item + "notes emptied",
            "Removals, which apply refuses without --allow-removals:",
            "  " + Item + "website address \"https://github.com/login\" removed",
            "  " + Item + "text field \"Plan\" changed",
            "  " + Item + "notes emptied",
            "Items changed: 1",
            "Removals: 3",
            "The dry run wrote nothing. Apply refuses these removals without --allow-removals.",
        ]);
        prompts.NewPasswordPrompts.ShouldBeEmpty();
        Directory.GetFiles(directory).ShouldBeEquivalentTo(before);
        string[] secrets = [NotesText, FieldValue, "fixture-preview-new-value", Password, TotpSecret, UriSecret, UriToken, "alice"];
        secrets.ShouldAllSatisfy(secret =>
        {
            run.Output.ShouldNotContain(secret, StringComparison.Ordinal);
            run.Error.ShouldNotContain(secret, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Dry_run_checks_the_output_and_says_that_apply_with_allow_removals_makes_the_removals()
    {
        var prompts = new TestPrompts(ExportPassword);
        var environment = prompts.Environment();
        (string vault, string catalog, string decisions) = await SharedCatalog(directory, environment, TestContext.Current.CancellationToken);
        await EditItem(catalog, item => item["fields"] = new JsonArray(), TestContext.Current.CancellationToken);
        string output = Path.Combine(directory, "organized.json");

        var run = await CommandLineRun.Invoke(environment, ["apply", vault, "--catalog", catalog, "--decisions", decisions, "-o", output, "--dry-run", "--allow-removals"], TestContext.Current.CancellationToken);
        var existing = await CommandLineRun.Invoke(environment, ["apply", vault, "--catalog", catalog, "--decisions", decisions, "-o", vault, "--dry-run"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("  " + Item + "text field \"Plan\" removed", StringComparison.Ordinal);
        run.Output.ShouldEndWith("The dry run wrote nothing. Apply with --allow-removals makes them." + Environment.NewLine, StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
        prompts.NewPasswordPrompts.ShouldBeEmpty();
        existing.ExitCode.ShouldBe(3);
        existing.Error.ShouldContain("may be the same file as an input file", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dry_run_of_an_unedited_catalog_lists_no_changes()
    {
        var environment = new TestPrompts(ExportPassword).Environment();
        (string vault, string catalog, string decisions) = await SharedCatalog(directory, environment, TestContext.Current.CancellationToken);

        var run = await CommandLineRun.Invoke(environment, ["apply", vault, "--catalog", catalog, "--decisions", decisions, "--dry-run"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        run.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ShouldBe(
        [
            "Changes: none",
            "Items changed: 0",
            "Removals: 0",
            "The dry run wrote nothing.",
        ]);
    }

    [Fact]
    public async Task Dry_run_of_a_catalog_with_a_problem_exits_3()
    {
        var environment = new TestPrompts(ExportPassword).Environment();
        (string vault, string catalog, string decisions) = await SharedCatalog(directory, environment, TestContext.Current.CancellationToken);
        await EditItem(catalog, item => item["name"] = " ", TestContext.Current.CancellationToken);

        var run = await CommandLineRun.Invoke(environment, ["apply", vault, "--catalog", catalog, "--decisions", decisions, "--dry-run"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("the name is empty", StringComparison.Ordinal);
        run.Output.ShouldNotContain("The dry run wrote nothing", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dry_run_of_a_name_over_the_bitwarden_limit_exits_3()
    {
        var environment = new TestPrompts(ExportPassword).Environment();
        (string vault, string catalog, string decisions) = await SharedCatalog(directory, environment, TestContext.Current.CancellationToken);
        await EditItem(catalog, item => item["name"] = new string('n', 1_001), TestContext.Current.CancellationToken);

        var run = await CommandLineRun.Invoke(environment, ["apply", vault, "--catalog", catalog, "--decisions", decisions, "--dry-run"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("too long for Bitwarden import", StringComparison.Ordinal);
        run.Output.ShouldNotContain("The dry run wrote nothing", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dry_run_shows_format_and_control_characters_of_a_name_escaped()
    {
        var environment = new TestPrompts(ExportPassword).Environment();
        (string vault, string catalog, string decisions) = await SharedCatalog(directory, environment, TestContext.Current.CancellationToken);
        await EditItem(catalog, item => item["name"] = "Git​Hub\u001B[31m", TestContext.Current.CancellationToken);

        var run = await CommandLineRun.Invoke(environment, ["apply", vault, "--catalog", catalog, "--decisions", decisions, "--dry-run"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("renamed to \"Git\\u200BHub�[31m\"", StringComparison.Ordinal);
        run.Output.ShouldNotContain("​", StringComparison.Ordinal);
        run.Output.ShouldNotContain("\u001B", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_refuses_removals_without_allow_removals_and_writes_nothing()
    {
        var prompts = new TestPrompts(ExportPassword);
        var environment = prompts.Environment();
        (string vault, string catalog, string decisions) = await SharedCatalog(directory, environment, TestContext.Current.CancellationToken);
        await EditItem(catalog, item => item["fields"] = new JsonArray(), TestContext.Current.CancellationToken);
        string output = Path.Combine(directory, "organized.json");

        var run = await CommandLineRun.Invoke(environment, ["apply", vault, "--catalog", catalog, "--decisions", decisions, "-o", output], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("Error: " + Item + "text field \"Plan\" removed", StringComparison.Ordinal);
        run.Error.ShouldContain("The vault was not written because the catalog removes what apply keeps unless --allow-removals is given. Check the changes with --dry-run.", StringComparison.Ordinal);
        run.Error.ShouldNotContain(FieldValue, StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
        prompts.NewPasswordPrompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Apply_with_allow_removals_writes_the_vault()
    {
        var environment = new TestPrompts(ExportPassword).Environment();
        (string vault, string catalog, string decisions) = await SharedCatalog(directory, environment, TestContext.Current.CancellationToken);
        await EditItem(catalog, item => item["fields"] = new JsonArray(), TestContext.Current.CancellationToken);
        string output = Path.Combine(directory, "organized.json");

        var run = await CommandLineRun.Invoke(environment, ["apply", vault, "--catalog", catalog, "--decisions", decisions, "-o", output, "--plaintext", "--allow-removals"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("Items changed: 1" + Environment.NewLine + "Removals: 1", StringComparison.Ordinal);
        var written = BitwardenJsonReader.Read(await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken), "organized.json");
        written.Items.ShouldHaveSingleItem().Fields.ShouldBeEmpty();
    }

    [Fact]
    public async Task Apply_without_output_or_dry_run_fails_parsing()
    {
        var environment = new TestPrompts(ExportPassword).Environment();
        (string vault, string catalog, _) = await SharedCatalog(directory, environment, TestContext.Current.CancellationToken);

        var run = await CommandLineRun.Invoke(environment, ["apply", vault, "--catalog", catalog], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("apply needs --output <file> unless --dry-run is given.", StringComparison.Ordinal);
    }
}
