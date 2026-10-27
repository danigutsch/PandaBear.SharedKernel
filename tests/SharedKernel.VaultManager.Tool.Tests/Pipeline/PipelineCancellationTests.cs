using PandaBear.VaultManager.Tool.Pipeline;
using static PandaBear.VaultManager.Tool.Tests.Pipeline.PipelineInputs;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

/// <summary>Tests that <see cref="VaultPipeline"/> stops with <see cref="OperationCanceledException"/> and leaves no output when its token is canceled.</summary>
public sealed class PipelineCancellationTests : IDisposable
{
    private const string Password = "fixture-output-password";
    private const string ExportPassword = "fixture export password";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-pipeline-cancellation-").FullName;

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);

    /// <summary>Canceling while the new-password prompt is shown makes Merge throw after exactly one prompt, leaving only the input file in the directory.</summary>
    [Fact]
    public void Merge_canceled_at_the_new_password_prompt_leaves_neither_a_temporary_file_nor_an_output()
    {
        // Arrange
        string input = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", Password, "https://example.com")));
        string output = Path.Combine(directory, "merged.json");
        using var cancellation = new CancellationTokenSource();
        var prompts = new TestPrompts(ExportPassword) { OnNewPassword = cancellation.Cancel };
        var pipeline = new VaultPipeline(prompts.Environment());
        var scanned = VaultPipeline.Scan(pipeline.Load([input], TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        var action = () => pipeline.Merge(scanned, new MergeRequest { Output = new PipelineOutput(output) }, cancellation.Token);

        // Act
        action.ShouldThrow<OperationCanceledException>();

        // Assert
        prompts.NewPasswordPrompts.ShouldHaveSingleItem();
        Directory.GetFileSystemEntries(directory).ShouldBe([input]);
    }

    /// <summary>Merge with an already canceled token and plaintext output throws before any password prompt and writes no file.</summary>
    [Fact]
    public void Merge_with_a_canceled_token_writes_nothing_and_asks_for_no_password()
    {
        // Arrange
        string input = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", Password, "https://example.com")));
        string output = Path.Combine(directory, "merged.json");
        var prompts = new TestPrompts(ExportPassword);
        var pipeline = new VaultPipeline(prompts.Environment());
        var scanned = VaultPipeline.Scan(pipeline.Load([input], TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => pipeline.Merge(scanned, new MergeRequest { Output = new PipelineOutput(output, Plaintext: true) }, cancellation.Token);

        // Act
        action.ShouldThrow<OperationCanceledException>();

        // Assert
        prompts.NewPasswordPrompts.ShouldBeEmpty();
        Directory.GetFileSystemEntries(directory).ShouldBe([input]);
    }

    /// <summary>Load with an already canceled token on a password-protected Bitwarden export throws without asking for the file password.</summary>
    [Fact]
    public void Load_with_a_canceled_token_reads_no_input_and_asks_for_no_password()
    {
        // Arrange
        string input = Path.Combine(AppContext.BaseDirectory, "Fixtures", "bitwarden", "protected-pbkdf2.json");
        var prompts = new TestPrompts(ExportPassword);
        var pipeline = new VaultPipeline(prompts.Environment());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => pipeline.Load([input], cancellation.Token);

        // Act
        action.ShouldThrow<OperationCanceledException>();

        // Assert
        prompts.FilePasswordPrompts.ShouldBeEmpty();
    }

    /// <summary>Scan with an already canceled token throws <see cref="OperationCanceledException"/> instead of scanning the loaded vault.</summary>
    [Fact]
    public void Scan_with_a_canceled_token_stops_before_scanning()
    {
        // Arrange
        string input = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", Password, "https://example.com")));
        var loaded = new VaultPipeline(new TestPrompts(ExportPassword).Environment()).Load([input], TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => VaultPipeline.Scan(loaded, cancellation.Token);

        // Act, Assert
        action.ShouldThrow<OperationCanceledException>();
    }
}
