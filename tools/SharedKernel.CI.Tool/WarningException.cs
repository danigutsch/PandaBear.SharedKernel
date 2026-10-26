namespace PandaBear.CI.Tool;

/// <summary>
/// One reviewed exception that lets a CI tool count a bounded set of findings as accepted instead of blocking or advisory.
/// </summary>
/// <param name="Tool">Names the producing tool, matching <see cref="WarningReport.Tool"/>.</param>
/// <param name="Rule">Names the exact rule or diagnostic, the first word of a finding.</param>
/// <param name="Scope">Names the subject the exception covers: the finding text after the rule starts with it.</param>
/// <param name="Baseline">Caps how many findings the exception accepts, so a baseline cannot absorb growth.</param>
/// <param name="Rationale">Explains why immediate remediation is unsuitable.</param>
/// <param name="Owner">Names the team responsible for the remediation.</param>
/// <param name="Expires">Last day the exception counts; afterwards it is reported and accepts nothing.</param>
internal sealed record WarningException(string Tool, string Rule, string Scope, int Baseline, string Rationale, string Owner, DateOnly Expires);
