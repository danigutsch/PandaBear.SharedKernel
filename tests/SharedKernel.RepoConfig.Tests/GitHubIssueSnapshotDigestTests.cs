namespace SharedKernel.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class GitHubIssueSnapshotDigestTests
{
    [Fact]
    public void Compute_distinguishes_label_boundaries_from_label_content()
    {
        // Arrange
        GitHubRoadmapReconcileIssue[] separateLabels =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Issue", "OPEN", labels: ["first", "second"])
        ];
        GitHubRoadmapReconcileIssue[] embeddedDelimiter =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Issue", "OPEN", labels: ["first\u001fsecond"])
        ];

        // Act
        var separateDigest = GitHubIssueSnapshotDigest.Compute(separateLabels);
        var embeddedDigest = GitHubIssueSnapshotDigest.Compute(embeddedDelimiter);

        // Assert
        separateDigest.ShouldNotBe(embeddedDigest);
    }

    [Fact]
    public void Compute_is_stable_across_issue_label_and_relation_ordering()
    {
        // Arrange
        var first = GitHubRoadmapSnapshotTestOperations.Issue(
            100,
            "First",
            "OPEN",
            labels: ["beta", "alpha"],
            subIssues:
            [
                GitHubRoadmapSnapshotTestOperations.Relation(102, "OPEN"),
                GitHubRoadmapSnapshotTestOperations.Relation(101, "OPEN")
            ]);
        var reorderedFirst = GitHubRoadmapSnapshotTestOperations.Issue(
            100,
            "First",
            "OPEN",
            labels: ["alpha", "beta"],
            subIssues:
            [
                GitHubRoadmapSnapshotTestOperations.Relation(101, "OPEN"),
                GitHubRoadmapSnapshotTestOperations.Relation(102, "OPEN")
            ]);
        var second = GitHubRoadmapSnapshotTestOperations.Issue(200, "Second", "OPEN");

        // Act
        var firstDigest = GitHubIssueSnapshotDigest.Compute([first, second]);
        var reorderedDigest = GitHubIssueSnapshotDigest.Compute([second, reorderedFirst]);

        // Assert
        reorderedDigest.ShouldBe(firstDigest);
    }
}
