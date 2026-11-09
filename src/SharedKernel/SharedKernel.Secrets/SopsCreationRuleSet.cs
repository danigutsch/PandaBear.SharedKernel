using PandaBear.Results;

namespace PandaBear.Secrets;

/// <summary>Ordered creation rules. The first rule that matches a path applies, as in SOPS.</summary>
public sealed class SopsCreationRuleSet
{
    private SopsCreationRuleSet(SopsCreationRule[] rules) => Rules = rules;

    /// <summary>Gets the rules in the order they are tried.</summary>
    public IReadOnlyList<SopsCreationRule> Rules
    {
        get;
    }

    /// <summary>Creates a set. The input may be empty but not null or contain a null.</summary>
    /// <param name="rules">The rules, in order.</param>
    /// <returns>The set or a validation error.</returns>
    public static Result<SopsCreationRuleSet> Create(IEnumerable<SopsCreationRule?>? rules)
    {
        if (rules is null)
        {
            return Result.Invalid<SopsCreationRuleSet>("Rules are required.", "rules", "Rules are required.");
        }

        var items = rules.ToArray();
        return items.Any(static rule => rule is null)
            ? Result.Invalid<SopsCreationRuleSet>("A rule is missing.", "rules", "A rule is missing.")
            : Result.Ok(new SopsCreationRuleSet([.. items.OfType<SopsCreationRule>()]));
    }

    /// <summary>Returns the first rule that matches <paramref name="path"/>, or <see langword="null"/> when none does.</summary>
    /// <param name="path">The relative file path.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public SopsCreationRule? FirstMatch(EncryptedFilePath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Rules.FirstOrDefault(rule => rule.Matches(path));
    }
}
