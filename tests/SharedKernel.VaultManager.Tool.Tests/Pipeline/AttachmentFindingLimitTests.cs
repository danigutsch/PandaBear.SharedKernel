using System.Globalization;
using PandaBear.VaultManager.Tool.Attachments;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Tests.Secrets;
using static PandaBear.VaultManager.Tool.Tests.Pipeline.PipelineInputs;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

public sealed class AttachmentFindingLimitTests : IDisposable
{
    private const string ExportPassword = "fixture export password";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-attachment-limit-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact(Timeout = LargeTexts.ScanBudget)]
    public async Task Scan_lists_at_most_the_finding_limit_of_a_large_attachment_and_warns_that_it_was_scanned_in_part()
    {
        var (text, answers) = LargeTexts.Questions(TextExtractor.MaxBytes - 4096);
        string zip = Write(directory, "keeper.zip", CommanderZip(
            KeeperJson(KeeperFileRecord("rec-files", "Door codes", "file-codes", "door-codes.txt")),
            ("files/file-codes", text)));
        var loaded = new VaultPipeline(new TestPrompts(ExportPassword).Environment()).Load([zip], TestContext.Current.CancellationToken);

        var scanned = await Task.Run(() => VaultPipeline.Scan(loaded, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        answers.Count.ShouldBeGreaterThan(VaultPipeline.MaxAttachmentFindings);
        var findings = scanned.Items.ShouldHaveSingleItem().Findings;
        findings.Count.ShouldBe(VaultPipeline.MaxAttachmentFindings);
        scanned.Warnings.ShouldBe(
        [
            string.Create(
                CultureInfo.InvariantCulture,
                $"\"Door codes\": attachment \"door-codes.txt\" was scanned only in part because it has more than {VaultPipeline.MaxAttachmentFindings} findings; only the first {VaultPipeline.MaxAttachmentFindings} are listed."),
        ]);
    }
}
