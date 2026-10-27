using PandaBear.VaultManager.Tool.Brief;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>Tests the brief that the merge command writes.</summary>
public sealed class MergeBriefCommandTests : IDisposable
{
    private const string ExportPassword = "fixture export password";
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly string directory = Directory.CreateTempSubdirectory("vault-cli-brief-").FullName;

    /// <summary>Deletes the temporary directory.</summary>
    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public async Task Merge_writes_the_brief_also_when_fields_need_a_decision()
    {
        // Arrange
        string[] inputs = CliInputs.ConflictingLogins(directory);
        string output = Path.Combine(directory, "merged.json");
        string decisionsPath = Path.Combine(directory, "decisions.json");
        string briefPath = Path.Combine(directory, "brief.json");

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, "--decisions", decisionsPath, "--brief", briefPath], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Output.ShouldContain("Brief: " + briefPath + "\n", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
        var brief = BriefFile.Read(briefPath);
        var group = brief.Groups.ShouldHaveSingleItem();
        group.Key.ShouldBe(DecisionsFile.Read(decisionsPath).Groups.ShouldHaveSingleItem().GroupKey);
        group.Fields.ShouldHaveSingleItem().FieldPath.ShouldBe(FieldPaths.LoginPassword);
        string json = await File.ReadAllTextAsync(briefPath, TestContext.Current.CancellationToken);
        json.ShouldNotContain(CliInputs.KeeperPassword, StringComparison.Ordinal);
        json.ShouldNotContain(CliInputs.BitwardenPassword, StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(briefPath).ShouldBe(OwnerOnly);
        }
    }

    [Fact]
    public async Task Merge_that_writes_the_vault_writes_an_empty_brief_and_replaces_an_earlier_one()
    {
        // Arrange
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "merged.json");
        string briefPath = Path.Combine(directory, "brief.json");
        string[] args = ["merge", .. inputs, "--output", output, "--brief", briefPath, "--plaintext", "--force"];

        // Act
        var first = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken);
        var second = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken);

        // Assert
        first.ExitCode.ShouldBe(0);
        second.ExitCode.ShouldBe(0);
        second.Output.ShouldContain("Brief: " + briefPath, StringComparison.Ordinal);
        var brief = BriefFile.Read(briefPath);
        brief.Groups.ShouldBeEmpty();
        brief.Summary.Items.ShouldBe(1);
    }

    [Fact]
    public async Task Merge_replaces_a_brief_that_an_editor_saved_with_a_byte_order_mark()
    {
        // Arrange
        string[] inputs = CliInputs.ConflictingLogins(directory);
        string output = Path.Combine(directory, "merged.json");
        string briefPath = Path.Combine(directory, "brief.json");
        string[] args = ["merge", .. inputs, "--output", output, "--brief", briefPath];
        (await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken)).ExitCode.ShouldBe(2);
        string text = await File.ReadAllTextAsync(briefPath, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(briefPath, text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true), TestContext.Current.CancellationToken);

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Output.ShouldContain("Brief: " + briefPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Merge_refuses_a_brief_path_that_names_a_file_that_is_not_a_brief()
    {
        // Arrange
        string[] inputs = CliInputs.ConflictingLogins(directory);
        string output = Path.Combine(directory, "merged.json");
        string briefPath = PipelineInputs.Write(directory, "notes.txt", "fixture text that is not a brief");
        var prompts = new TestPrompts(ExportPassword);

        // Act
        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", .. inputs, "--output", output, "--brief", briefPath], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("exists and is not a brief", StringComparison.Ordinal);
        string after = await File.ReadAllTextAsync(briefPath, TestContext.Current.CancellationToken);
        after.ShouldBe("fixture text that is not a brief");
        prompts.NewPasswordPrompts.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("output")]
    [InlineData("decisions")]
    [InlineData("report")]
    [InlineData("input")]
    public async Task Merge_refuses_a_brief_path_that_is_an_input_or_another_file_it_writes(string other)
    {
        // Arrange
        string[] inputs = CliInputs.ConflictingLogins(directory);
        string output = Path.Combine(directory, "merged.json");
        string decisionsPath = Path.Combine(directory, "decisions.json");
        string report = Path.Combine(directory, "report.md");
        string briefPath = other switch
        {
            "output" => output,
            "decisions" => decisionsPath,
            "report" => report,
            _ => inputs[0],
        };

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, "--decisions", decisionsPath, "--report", report, "--brief", briefPath], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("may be the same file as", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
        File.Exists(decisionsPath).ShouldBeFalse();
    }
}
