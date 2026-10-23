using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using static PandaBear.VaultManager.Tool.Tests.Merge.MergeFixture;

namespace PandaBear.VaultManager.Tool.Tests.Merge;

public sealed class MergeReportTests
{
    [Fact]
    public void Report_markdown_contains_no_values()
    {
        var keeper = Keeper(Login("fixture-old-password", "JBSWY3DPEHPK3PXP")) with { Notes = "fixture-old-notes", FolderPath = "Personal" };
        var bitwarden = Bitwarden(Login("fixture-new-password")) with { Notes = "fixture-new-notes", FolderPath = "Work" };
        var unknown = WithoutTimestamp(Keeper(Login("fixture-other-password") with { Uris = [new LoginUri("https://bank.example.org")] }) with
        {
            Name = "Bank",
            Origin = new ItemOrigin("keeper", KeeperFile, "k2", null),
        });
        var unknownDuplicate = WithoutTimestamp(Bitwarden(Login("fixture-third-password") with { Uris = [new LoginUri("https://bank.example.org")] }) with
        {
            Name = "Bank",
            Origin = new ItemOrigin("bitwarden", BitwardenFile, "b2", null),
        });

        var report = Run(keeper, bitwarden, unknown, unknownDuplicate).Report;
        string markdown = report.ToMarkdown();

        report.DifferentGroups.ShouldBe(2);
        report.AutoResolvedConflicts.ShouldBe(3);
        report.UnresolvedConflicts.ShouldBe(1);
        markdown.ShouldContain("Example", StringComparison.Ordinal);
        markdown.ShouldContain(FieldPaths.LoginPassword, StringComparison.Ordinal);
        markdown.ShouldContain("unresolved", StringComparison.Ordinal);
        string[] values = ["fixture-old-password", "fixture-new-password", "fixture-other-password", "fixture-third-password", "JBSWY3DPEHPK3PXP", "fixture-old-notes", "fixture-new-notes"];
        values.ShouldAllSatisfy(value => markdown.ShouldNotContain(value, StringComparison.Ordinal));
    }
}
