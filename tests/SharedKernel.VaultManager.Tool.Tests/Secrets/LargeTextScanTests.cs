using PandaBear.VaultManager.Tool.Attachments;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>
/// Scans texts as large as the attachment text limit. Every secret of the text is expected to be reported, or to lie in
/// text that a pattern timeout flagged for review (<see cref="ScanCoverage"/>), and the timeout,
/// <see cref="LargeTexts.ScanBudget"/>, is an upper bound for near-linear scanning on a busy machine; work that grows with
/// the square of the text never finishes in it.
/// </summary>
public sealed class LargeTextScanTests
{
    private const string FieldPath = "attachment:notes.txt";

    [Fact(Timeout = LargeTexts.ScanBudget)]
    public async Task ScanText_finds_every_question_and_answer_pair_of_a_large_text()
    {
        var (text, answers) = LargeTexts.Questions(TextExtractor.MaxBytes);

        var findings = await Task.Run(() => SecretScanner.Default.ScanText(text, FieldPath, "notes.txt"), TestContext.Current.CancellationToken);

        ScanCoverage.ShouldReportEverySecretOrFlagItForReview(findings, answers, SecretKind.SecurityQuestion);
    }

    [Fact(Timeout = LargeTexts.ScanBudget)]
    public async Task ScanText_finds_every_access_key_ID_of_a_large_log()
    {
        var (text, keys) = LargeTexts.AccessKeyLog(TextExtractor.MaxBytes);

        var findings = await Task.Run(() => SecretScanner.Default.ScanText(text, FieldPath, "access.log"), TestContext.Current.CancellationToken);

        ScanCoverage.ShouldReportEverySecretOrFlagItForReview(findings, keys, SecretKind.AccessKeyId);
    }

    [Fact(Timeout = LargeTexts.ScanBudget)]
    public async Task ScanText_finds_questions_separated_by_long_runs_of_blank_lines()
    {
        var (text, answers) = LargeTexts.QuestionsBetweenBlankLines(TextExtractor.MaxBytes, pairs: 20);

        var findings = await Task.Run(() => SecretScanner.Default.ScanText(text, FieldPath, "notes.txt"), TestContext.Current.CancellationToken);

        ScanCoverage.ShouldReportEverySecretOrFlagItForReview(findings, answers, SecretKind.SecurityQuestion);
    }
}
