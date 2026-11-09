namespace PandaBear.Secrets.Tool;

/// <summary>The options of <c>verify</c>.</summary>
/// <param name="AllowUnverifiedFiles">Do not fail because some SOPS files match no creation rule. The risk is that nobody checks who can read them.</param>
/// <param name="BaseRevision">A git revision to compare each label's enc with, or <see langword="null"/>.</param>
/// <param name="AllowChangedRecipients">Do not fail because a label's enc is new or different from the base revision's.</param>
internal sealed record VerifyOptions(bool AllowUnverifiedFiles, string? BaseRevision, bool AllowChangedRecipients);
