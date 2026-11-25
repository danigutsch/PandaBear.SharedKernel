# SharedKernel.Review

Records append-only evidence for human review and automated advisory checks on
versioned content. A history holds the current revision, the configured human lanes,
and evidence across every revision. Each successful operation returns a new history,
so consumers can preserve prior snapshots.

Configure the required lanes in their approval order. An authorized human reviewer
decides for the expected lane. Approval advances to the next lane. A request for
changes ends human review of that revision. Starting a greater revision number
resets the lane order and retains past evidence, even when an edit restores the bytes
of an earlier revision.

```csharp
var revision = new ReviewRevision(1, canonicalContentSha256);
var history = ReviewHistory.Start(revision, ["technical", "release"]);

var advisory = history.RecordAdvisory(revision, "technical", toolPseudonym,
    timeProvider.GetUtcNow(), "reference-check");
var reviewed = advisory.Value.RecordHumanDecision(revision, "technical",
    ReviewDecision.Approved, reviewerPseudonym, timeProvider.GetUtcNow(), "verified");
```

The human and advisory methods take the reviewed revision number and digest.
A stale number or a different digest returns a conflict. Only the human decision
method advances a lane. Advisory evidence can document a finding in a configured lane
without granting approval, editing content, signing, or publishing.

The host authenticates and authorizes human reviewers, decides which automated
checks may record evidence, and defines the reason codes and pseudonyms. Use bounded
identifiers and reserve free-form details for the host's protected records. Allocate
a new increasing review revision number whenever endorsed content changes and hash
the exact canonical content the reviewer saw. Persist history with a host-owned
concurrency guard so a decision recorded against an older snapshot cannot overwrite
newer state. The host also owns retention, publication, and signature policies.

The package ID is `PandaBear.SharedKernel.Review` and the namespace is `SharedKernel.Review`.
