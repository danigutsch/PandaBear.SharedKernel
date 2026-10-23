using System.Text.Json.Nodes;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

public sealed class CatalogCommandTests : IDisposable
{
    private const string Password = "fixture-catalog-cli-password";
    private const string NotesText = "fixture catalog cli notes";
    private const string ExportPassword = "fixture export password";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-cli-catalog-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public async Task Catalog_then_apply_renames_and_moves_an_item_without_showing_secrets()
    {
        // Arrange
        string vault = CliInputs.LoginWithNotesInFolder(directory, Password, NotesText);
        string catalogPath = Path.Combine(directory, "catalog.json");
        string decisionsPath = Path.Combine(directory, "decisions.json");
        string output = Path.Combine(directory, "organized.json");
        var environment = new TestPrompts(ExportPassword).Environment();

        // Act
        var catalogRun = await CommandLineRun.Invoke(environment, ["catalog", vault, "-o", catalogPath, "--decisions", decisionsPath], TestContext.Current.CancellationToken);
        string catalogText = await File.ReadAllTextAsync(catalogPath, TestContext.Current.CancellationToken);
        var catalogNotes = DecisionsFile.Read(decisionsPath).Notes;
        var document = JsonNode.Parse(catalogText).ShouldNotBeNull();
        var item = document["items"].ShouldNotBeNull()[0].ShouldNotBeNull();
        item["name"] = "GitHub (work)";
        item["folder"] = "Work/Code";
        await File.WriteAllTextAsync(catalogPath, document.ToJsonString(), TestContext.Current.CancellationToken);
        var applyRun = await CommandLineRun.Invoke(environment, ["apply", vault, "--catalog", catalogPath, "-o", output, "--decisions", decisionsPath, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        catalogRun.ExitCode.ShouldBe(0);
        catalogRun.Output.ShouldContain("Notes withheld until you decide: 1", StringComparison.Ordinal);
        string[] secrets = [Password, NotesText];
        secrets.ShouldAllSatisfy(secret =>
        {
            catalogText.ShouldNotContain(secret, StringComparison.Ordinal);
            catalogRun.Output.ShouldNotContain(secret, StringComparison.Ordinal);
            catalogRun.Error.ShouldNotContain(secret, StringComparison.Ordinal);
        });
        catalogNotes.ShouldHaveSingleItem().Decision.ShouldBe(NotesDecision.Pending);
        applyRun.ExitCode.ShouldBe(0);
        applyRun.Output.ShouldContain("Items changed: 1", StringComparison.Ordinal);
        var written = BitwardenJsonReader.Read(await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken), "organized.json");
        var applied = written.Items.ShouldHaveSingleItem();
        applied.Name.ShouldBe("GitHub (work)");
        applied.FolderPath.ShouldBe("Work/Code");
        applied.Notes.ShouldBe(NotesText);
        applied.Login.ShouldNotBeNull().Password.ShouldBe(Password);
        written.Folders.ShouldBe(["Work", "Work/Code"]);
    }

    [Fact]
    public async Task Apply_refuses_a_catalog_made_from_another_version_of_the_vault()
    {
        // Arrange
        string vault = CliInputs.LoginWithNotesInFolder(directory, Password, NotesText);
        string catalogPath = Path.Combine(directory, "catalog.json");
        string output = Path.Combine(directory, "organized.json");
        var environment = new TestPrompts(ExportPassword).Environment();
        (await CommandLineRun.Invoke(environment, ["catalog", vault, "-o", catalogPath], TestContext.Current.CancellationToken)).ExitCode.ShouldBe(0);
        await File.AppendAllTextAsync(vault, " ", TestContext.Current.CancellationToken);

        // Act
        var run = await CommandLineRun.Invoke(environment, ["apply", vault, "--catalog", catalogPath, "-o", output, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("changed after the catalog was made", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public async Task Apply_writes_nothing_while_the_catalog_sets_withheld_notes()
    {
        // Arrange
        string vault = CliInputs.LoginWithNotesInFolder(directory, Password, NotesText);
        string catalogPath = Path.Combine(directory, "catalog.json");
        string output = Path.Combine(directory, "organized.json");
        var environment = new TestPrompts(ExportPassword).Environment();
        (await CommandLineRun.Invoke(environment, ["catalog", vault, "-o", catalogPath], TestContext.Current.CancellationToken)).ExitCode.ShouldBe(0);
        var document = JsonNode.Parse(await File.ReadAllTextAsync(catalogPath, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        document["items"].ShouldNotBeNull()[0].ShouldNotBeNull()["notes"] = new JsonObject { ["decision"] = "Share", ["text"] = "replaced" };
        await File.WriteAllTextAsync(catalogPath, document.ToJsonString(), TestContext.Current.CancellationToken);

        // Act
        var run = await CommandLineRun.Invoke(environment, ["apply", vault, "--catalog", catalogPath, "-o", output, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("the decisions file does not", StringComparison.Ordinal);
        run.Error.ShouldNotContain(NotesText, StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public async Task Apply_names_the_attachments_to_upload_only_when_it_writes_the_vault()
    {
        // Arrange
        string vault = CliInputs.KeeperZipWithFolderAndAttachments(directory);
        string catalogPath = Path.Combine(directory, "catalog.json");
        string output = Path.Combine(directory, "organized.json");
        var environment = new TestPrompts(ExportPassword).Environment();
        (await CommandLineRun.Invoke(environment, ["catalog", vault, "-o", catalogPath], TestContext.Current.CancellationToken)).ExitCode.ShouldBe(0);
        var document = JsonNode.Parse(await File.ReadAllTextAsync(catalogPath, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var backup = document["items"].ShouldNotBeNull().AsArray().ShouldHaveSingleItem(item => item!["name"]!.GetValue<string>() == "Backup").ShouldNotBeNull();
        backup["name"] = new string('x', 1001);
        await File.WriteAllTextAsync(catalogPath, document.ToJsonString(), TestContext.Current.CancellationToken);

        // Act
        var run = await CommandLineRun.Invoke(environment, ["apply", vault, "--catalog", catalogPath, "-o", output, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("Bitwarden would reject the import", StringComparison.Ordinal);
        run.Error.ShouldNotContain("is not in the written vault", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
    }
}
