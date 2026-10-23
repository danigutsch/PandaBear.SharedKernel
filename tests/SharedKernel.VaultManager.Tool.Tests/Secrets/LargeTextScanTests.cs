using PandaBear.VaultManager.Tool.Attachments;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>
/// Scans texts as large as the attachment text limit. Every finding of the text is expected, and the timeout is a
/// generous upper bound for near-linear scanning; work that grows with the square of the text never finishes in it.
/// </summary>
public sealed class LargeTextScanTests
{
    private const int Budget = 120_000;
    private const string FieldPath = "attachment:notes.txt";

    [Fact(Timeout = Budget)]
    public async Task ScanText_finds_every_question_and_answer_pair_of_a_large_text()
    {
        var (text, expected) = LargeTexts.Questions(TextExtractor.MaxBytes);

        var findings = await Task.Run(() => SecretScanner.Default.ScanText(text, FieldPath, "notes.txt"), TestContext.Current.CancellationToken);

        findings.Count.ShouldBe(expected);
        findings.ShouldAllSatisfy(finding => finding.Kind.ShouldBe(SecretKind.SecurityQuestion));
    }

    [Fact(Timeout = Budget)]
    public async Task ScanText_finds_every_access_key_ID_of_a_large_log()
    {
        var (text, expected) = LargeTexts.AccessKeyLog(TextExtractor.MaxBytes);

        var findings = await Task.Run(() => SecretScanner.Default.ScanText(text, FieldPath, "access.log"), TestContext.Current.CancellationToken);

        findings.Count.ShouldBe(expected);
        findings.ShouldAllSatisfy(finding => finding.Kind.ShouldBe(SecretKind.AccessKeyId));
    }

    [Fact(Timeout = Budget)]
    public async Task ScanText_finds_questions_separated_by_long_runs_of_blank_lines()
    {
        var (text, expected) = LargeTexts.QuestionsBetweenBlankLines(TextExtractor.MaxBytes, pairs: 20);

        var findings = await Task.Run(() => SecretScanner.Default.ScanText(text, FieldPath, "notes.txt"), TestContext.Current.CancellationToken);

        findings.Count.ShouldBe(expected);
        findings.ShouldAllSatisfy(finding => finding.Kind.ShouldBe(SecretKind.SecurityQuestion));
    }
}
