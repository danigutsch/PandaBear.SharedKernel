using System.Globalization;
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

    [Theory]
    [InlineData(5)]
    [InlineData(1000)]
    public void Password_health_lists_at_most_three_other_items_for_each_item_that_shares_a_password(int count)
    {
        VaultItem[] items =
        [
            .. Enumerable.Range(1, count).Select(number => Item(
                string.Create(CultureInfo.InvariantCulture, $"Site {number}"),
                "alice",
                Reused,
                string.Create(CultureInfo.InvariantCulture, $"https://site{number}.example.com"))),
        ];

        var issues = PasswordHealthReport.Analyze(new VaultExport(items)).Issues;

        issues.Count.ShouldBe(count);
        issues.ShouldAllSatisfy(issue => issue.Kind.ShouldBe(PasswordIssueKind.Reused));
        issues.Sum(issue => issue.RelatedItemNames.Count).ShouldBe(PasswordHealthReport.MaxRelatedItemNames * count);
        issues.ShouldAllSatisfy(issue => issue.RelatedItemCount.ShouldBe(count - 1));
        issues[0].RelatedItemNames.ShouldBe(["Site 2", "Site 3", "Site 4"]);
        issues[1].RelatedItemNames.ShouldBe(["Site 1", "Site 3", "Site 4"]);
    }

    [Fact]
    public void Password_health_counts_the_items_on_other_hosts_when_most_items_share_one_host()
    {
        VaultItem[] items =
        [
            .. Enumerable.Range(1, 200).Select(number => Item(string.Create(CultureInfo.InvariantCulture, $"Mail {number}"), "alice", Reused, "https://mail.example.com")),
            Item("Bank", "alice", Reused, "https://bank.example.org"),
            Item("Shop", "alice", Reused, "https://shop.example.org"),
        ];

        var issues = PasswordHealthReport.Analyze(new VaultExport(items)).Issues;

        issues.Count.ShouldBe(202);
        issues.Take(200).ShouldAllSatisfy(issue =>
        {
            issue.RelatedItemNames.ShouldBe(["Bank", "Shop"]);
            issue.RelatedItemCount.ShouldBe(2);
        });
        var bank = issues.ShouldHaveSingleItem(issue => issue.ItemName == "Bank");
        bank.RelatedItemNames.ShouldBe(["Mail 1", "Mail 2", "Mail 3"]);
        bank.RelatedItemCount.ShouldBe(201);
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
