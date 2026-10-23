namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>User decision on one secret finding. It names the finding, never its value.</summary>
/// <param name="FindingId">Finding identifier, as in <see cref="Secrets.SecretFinding.Id"/>.</param>
/// <param name="Decision">Whether the finding is applied.</param>
internal sealed record FindingChoice(string FindingId, FindingDecision Decision);
