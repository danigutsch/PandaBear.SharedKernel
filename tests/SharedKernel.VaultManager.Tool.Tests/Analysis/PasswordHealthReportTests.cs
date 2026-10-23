using PandaBear.VaultManager.Tool.Analysis;
using PandaBear.VaultManager.Tool.Model;
using static PandaBear.VaultManager.Tool.Tests.Analysis.PasswordHealthFixture;

namespace PandaBear.VaultManager.Tool.Tests.Analysis;

public sealed class PasswordHealthReportTests
{
    private const string Reused = "Fixture-Reused-Password-1";

    [Fact]
    public void Password_health_flags_reuse_across_hosts_and_password_equal_to_username()
    {
        VaultItem[] items =
        [
            Item("Mail", "alice", Reused, "https://mail.example.com"),
            Item("Mail copy", "alice", Reused, "https://www.mail.example.com/inbox"),
            Item("Bank", "alice", Reused, "https://bank.example.org"),
            Item("Router", "admin-account", "admin-account", "http://192.168.1.1"),
            Item("Strong", "bob", "Unique-Strong-Password-9", "https://strong.example.com"),
            Item("Weak", "carol", "abc", "https://weak.example.com"),
        ];

        var issues = PasswordHealthReport.Analyze(new VaultExport(items)).Issues;

        var mailReuse = issues.ShouldHaveSingleItem(issue => issue.ItemName == "Mail" && issue.Kind == PasswordIssueKind.Reused);
        mailReuse.RelatedItemNames.ShouldBe(["Bank"]);
        issues.ShouldHaveSingleItem(issue => issue.ItemName == "Bank" && issue.Kind == PasswordIssueKind.Reused).RelatedItemNames.ShouldBe(["Mail", "Mail copy"]);
        issues.ShouldContain(issue => issue.ItemName == "Router" && issue.Kind == PasswordIssueKind.EasilyGuessed);
        issues.ShouldContain(issue => issue.ItemName == "Weak" && issue.Kind == PasswordIssueKind.TooShort);
        issues.ShouldContain(issue => issue.ItemName == "Weak" && issue.Kind == PasswordIssueKind.SingleCharacterClass);
        issues.ShouldNotContain(issue => issue.ItemName == "Strong");
        issues.ShouldAllSatisfy(issue => issue.ToString().ShouldNotContain(Reused, StringComparison.Ordinal));
    }

    [Fact]
    public void Password_health_reports_a_password_equal_to_the_item_name_or_user_name_once_as_easily_guessed()
    {
        VaultItem[] items =
        [
            Item("Router-Admin-Panel", "admin", "router-admin-panel", "http://192.168.1.1"),
            Item("Printer", "printer-admin-01", "Printer-Admin-01", "http://192.168.1.2"),
            Item("Camera-Admin-Panel", "Camera-Admin-Panel", "Camera-Admin-Panel", "http://192.168.1.3"),
        ];

        var issues = PasswordHealthReport.Analyze(new VaultExport(items)).Issues;

        issues.Select(issue => (issue.ItemName, issue.Kind)).ShouldBe(
        [
            ("Router-Admin-Panel", PasswordIssueKind.EasilyGuessed),
            ("Printer", PasswordIssueKind.EasilyGuessed),
            ("Camera-Admin-Panel", PasswordIssueKind.EasilyGuessed),
        ]);
    }
}
