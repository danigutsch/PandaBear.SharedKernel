namespace PandaBear.Secrets;

/// <summary>One creation rule in <c>.config/sharedkernel-secrets.json</c>.</summary>
/// <param name="PathRegex">The unanchored path expression, compiled non-backtracking.</param>
/// <param name="Recipients">The exact recipients: public age recipients or SSH recipient lines.</param>
/// <param name="MinimumRecipients">The least number of recipients the rule requires.</param>
internal sealed record CreationRuleConfiguration(string PathRegex, string[] Recipients, int MinimumRecipients);
