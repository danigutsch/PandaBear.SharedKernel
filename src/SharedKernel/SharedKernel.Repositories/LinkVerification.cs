namespace SharedKernel.Repositories;

/// <summary>The outcome of <see cref="LinkVerifier.Verify(string, IReadOnlyList{string}, CancellationToken)"/>.</summary>
/// <param name="FileCount">How many documentation files were scanned.</param>
/// <param name="Issues">The broken links, anchors, and issue references in path order.</param>
public sealed record LinkVerification(int FileCount, IReadOnlyList<VerificationIssue> Issues);
