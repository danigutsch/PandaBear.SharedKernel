namespace PandaBear.Repositories;

/// <summary>
/// What a static evaluation of one project file found.
/// </summary>
/// <param name="TargetFramework">The <c>TargetFramework</c> value, or an empty string when it is unset.</param>
/// <param name="TargetFrameworks">The <c>TargetFrameworks</c> value, or an empty string when it is unset.</param>
/// <param name="TargetFrameworkFile">The repository-relative file that last assigned a non-empty <c>TargetFramework</c>, or an empty string.</param>
/// <param name="RollForward">The <c>RollForward</c> value, or an empty string when it is unset.</param>
/// <param name="Issues">The reasons that a value cannot be trusted; the other members are then unreliable.</param>
internal sealed record TargetFrameworkEvaluation(
    string TargetFramework,
    string TargetFrameworks,
    string TargetFrameworkFile,
    string RollForward,
    IReadOnlyList<VerificationIssue> Issues)
{
    /// <summary>
    /// Returns the frameworks that <c>TargetFrameworks</c> lists: split on <c>;</c>, trimmed, without empty entries or
    /// repeats that differ only in case, in order.
    /// </summary>
    public IReadOnlyList<string> Listed() => Split(TargetFrameworks);

    /// <summary>
    /// Returns the frameworks that the project declares: the <c>TargetFrameworks</c> list, or <c>TargetFramework</c> when
    /// the list is empty.
    /// </summary>
    public IReadOnlyList<string> Declared() => Listed() is { Count: > 0 } listed ? listed : Split(TargetFramework);

    /// <summary>
    /// Returns the frameworks that the project builds: <c>TargetFramework</c> when it is set, because it turns
    /// cross-targeting off, otherwise the <c>TargetFrameworks</c> list.
    /// </summary>
    public IReadOnlyList<string> Effective() => TargetFramework.Length > 0 ? [TargetFramework] : Listed();

    private static string[] Split(string value) =>
        value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
