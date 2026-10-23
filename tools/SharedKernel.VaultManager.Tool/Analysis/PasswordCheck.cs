namespace PandaBear.VaultManager.Tool.Analysis;

/// <summary>
/// How the password of one member of a match group compares, for a reader who decides between the members without seeing
/// it. Holds flags only, never the password, its length, or a hash.
/// </summary>
/// <param name="Weak">Whether <see cref="PasswordHealthReport.Analyze"/> would report the password as weak.</param>
/// <param name="Reused">Whether a member of another group, on other hosts, has the same password.</param>
/// <param name="InOtherHistory">Whether the password is in the password history of another member of the same group.</param>
internal sealed record PasswordCheck(bool Weak, bool Reused, bool InOtherHistory);
