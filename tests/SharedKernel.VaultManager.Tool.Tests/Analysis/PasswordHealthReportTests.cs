using System.Globalization;
using PandaBear.VaultManager.Tool.Analysis;
using PandaBear.VaultManager.Tool.Model;
using static PandaBear.VaultManager.Tool.Tests.Analysis.PasswordHealthFixture;

namespace PandaBear.VaultManager.Tool.Tests.Analysis;

/// <summary>Tests the password health findings: reuse, weak passwords, and passwords equal to the item name or user name.</summary>
public sealed class PasswordHealthReportTests
{
    private const string Reused = "Fixture-Reused-Password-1";

    [Fact]
    public void Password_health_flags_reuse_across_hosts_and_password_equal_to_username()
    {
        // Arrange
        VaultItem[] items =
        [
            Item("Mail", "alice", Reused, "https://mail.example.com"),
            Item("Mail copy", "alice", Reused, "https://www.mail.example.com/inbox"),
            Item("Bank", "alice", Reused, "https://bank.example.org"),
            Item("Router", "admin-account", "admin-account", "http://192.168.1.1"),
            Item("Strong", "bob", "Unique-Strong-Password-9", "https://strong.example.com"),
            Item("Weak", "carol", "abc", "https://weak.example.com"),
        ];

        // Act
        var issues = PasswordHealthReport.Analyze(new VaultExport(items)).Issues;

        // Assert
        var mailReuse = issues.ShouldHaveSingleItem(issue => issue.ItemName == "Mail" && issue.Kind == PasswordIssueKind.Reused);
        mailReuse.RelatedItemNames.ShouldBe(["Bank"]);
        issues.ShouldHaveSingleItem(issue => issue.ItemName == "Bank" && issue.Kind == PasswordIssueKind.Reused).RelatedItemNames.ShouldBe(["Mail", "Mail copy"]);
        issues.ShouldHaveSingleItem(issue => issue.ItemName == "Router").Kind.ShouldBe(PasswordIssueKind.Weak);
        issues.ShouldHaveSingleItem(issue => issue.ItemName == "Weak").Kind.ShouldBe(PasswordIssueKind.Weak);
        issues.ShouldNotContain(issue => issue.ItemName == "Strong");
        issues.ShouldAllSatisfy(issue => issue.ToString().ShouldNotContain(Reused, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(1000)]
    public void Password_health_lists_at_most_three_other_items_for_each_item_that_shares_a_password(int count)
    {
        // Arrange
        VaultItem[] items =
        [
            .. Enumerable.Range(1, count).Select(number => Item(
                string.Create(CultureInfo.InvariantCulture, $"Site {number}"),
                "alice",
                Reused,
                string.Create(CultureInfo.InvariantCulture, $"https://site{number}.example.com"))),
        ];

        // Act
        var issues = PasswordHealthReport.Analyze(new VaultExport(items)).Issues;

        // Assert
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
        // Arrange
        VaultItem[] items =
        [
            .. Enumerable.Range(1, 200).Select(number => Item(string.Create(CultureInfo.InvariantCulture, $"Mail {number}"), "alice", Reused, "https://mail.example.com")),
            Item("Bank", "alice", Reused, "https://bank.example.org"),
            Item("Shop", "alice", Reused, "https://shop.example.org"),
        ];

        // Act
        var issues = PasswordHealthReport.Analyze(new VaultExport(items)).Issues;

        // Assert
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
    public void Password_health_reports_a_password_equal_to_the_item_name_or_user_name_once_as_weak()
    {
        // Arrange
        VaultItem[] items =
        [
            Item("Router-Admin-Panel", "admin", "router-admin-panel", "http://192.168.1.1"),
            Item("Printer", "printer-admin-01", "Printer-Admin-01", "http://192.168.1.2"),
            Item("Camera-Admin-Panel", "Camera-Admin-Panel", "Camera-Admin-Panel", "http://192.168.1.3"),
        ];

        // Act
        var issues = PasswordHealthReport.Analyze(new VaultExport(items)).Issues;

        // Assert
        issues.Select(issue => (issue.ItemName, issue.Kind)).ShouldBe(
        [
            ("Router-Admin-Panel", PasswordIssueKind.Weak),
            ("Printer", PasswordIssueKind.Weak),
            ("Camera-Admin-Panel", PasswordIssueKind.Weak),
        ]);
    }

    [Fact]
    public void Password_health_reports_a_short_single_class_password_equal_to_the_user_name_as_one_weak_issue()
    {
        // Arrange
        VaultItem[] items = [Item("Router", "admin", "admin", "http://192.168.1.1")];

        // Act
        var issues = PasswordHealthReport.Analyze(new VaultExport(items)).Issues;

        // Assert
        var issue = issues.ShouldHaveSingleItem();
        issue.ItemName.ShouldBe("Router");
        issue.Kind.ShouldBe(PasswordIssueKind.Weak);
        issue.RelatedItemNames.ShouldBeEmpty();
        issue.RelatedItemCount.ShouldBe(0);
    }

    [Fact]
    public void Member_checks_count_reuse_only_by_members_of_other_groups_on_other_hosts()
    {
        // Arrange
        VaultItem keeper = Item("Mail", "alice", Reused, "https://mail.example.com");
        VaultItem bitwarden = Item("Mail (Bitwarden)", "alice", Reused, "https://mail.example.com");
        VaultItem bank = Item("Bank", "alice", Reused, "https://bank.example.org");
        VaultItem sameHost = Item("Mail alias", "bob", "Other-Strong-Password-7", "https://mail.example.com");
        VaultItem strong = Item("Strong", "bob", "Unique-Strong-Password-9", "https://strong.example.com");

        // Act
        var checks = PasswordHealthReport.CheckMembers([[keeper, bitwarden], [bank], [sameHost], [strong]]);

        // Assert
        checks.Select(group => group.Select(check => check.Reused).ToArray()).ShouldBe([[true, true], [true], [false], [false]]);
        checks.SelectMany(group => group).ShouldAllSatisfy(check => check.Weak.ShouldBeFalse());
    }

    [Fact]
    public void Member_checks_do_not_count_members_of_one_group_as_reuse_on_other_hosts()
    {
        // Arrange
        VaultItem first = Item("Mail", "alice", Reused, "https://mail.example.com");
        VaultItem second = Item("Mail", "alice", Reused, "https://webmail.example.net");
        VaultItem sameHostElsewhere = Item("Mail too", "alice", Reused, "https://mail.example.com");

        // Act
        var checks = PasswordHealthReport.CheckMembers([[first, second], [sameHostElsewhere]]);

        // Assert
        checks[0].Select(check => check.Reused).ShouldBe([false, true], "The first member shares its password only with its own group and with an item on its own host.");
        checks[1].ShouldHaveSingleItem().Reused.ShouldBeTrue();
    }

    [Fact]
    public void Member_checks_find_weak_passwords_and_passwords_in_another_member_history()
    {
        // Arrange
        VaultItem older = Item("Router", "admin", "Older-Strong-Password-1", "http://192.168.1.1");
        VaultItem newer = Item("Router", "admin", "admin", "http://192.168.1.1") with
        {
            PasswordHistory = [new PasswordHistoryEntry("Older-Strong-Password-1", DateTimeOffset.UnixEpoch)],
        };
        VaultItem note = new()
        {
            Type = VaultItemType.SecureNote,
            Name = "Note",
            Origin = new ItemOrigin("bitwarden", "vault.json", "note", null),
        };

        // Act
        var checks = PasswordHealthReport.CheckMembers([[older, newer, note]]).ShouldHaveSingleItem();

        // Assert
        checks.ShouldBe(
        [
            new PasswordCheck(Weak: false, Reused: false, InOtherHistory: true),
            new PasswordCheck(Weak: true, Reused: false, InOtherHistory: false),
            new PasswordCheck(Weak: false, Reused: false, InOtherHistory: false),
        ]);
    }
}
