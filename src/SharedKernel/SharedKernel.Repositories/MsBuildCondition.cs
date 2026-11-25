using System.Text.RegularExpressions;

namespace SharedKernel.Repositories;

/// <summary>
/// Evaluates the small subset of MSBuild conditions that the target framework check understands: terms of the form
/// <c>'a' == 'b'</c> and <c>'a' != 'b'</c>, joined by <c>and</c> and <c>or</c>, where <c>and</c> binds tighter and
/// there are no parentheses. Operands are single-quoted; <c>$(Name)</c> references are replaced by property values and
/// operands compare case-insensitively.
/// </summary>
internal static partial class MsBuildCondition
{
    /// <summary>
    /// Evaluates <paramref name="condition"/>, or returns <see langword="null"/> when it uses anything else, such as
    /// <c>Exists(...)</c>, a property function, parentheses, or an unquoted operand. An absent or blank condition is true.
    /// </summary>
    /// <param name="condition">The condition text.</param>
    /// <param name="substitute">Replaces the <c>$(Name)</c> references of an operand with their values.</param>
    public static bool? Evaluate(string? condition, Func<string, string> substitute)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        if (!Whole().IsMatch(condition))
        {
            return null;
        }

        var any = false;
        var all = true;
        foreach (Match token in Token().Matches(condition))
        {
            if (token.Groups["term"].Success)
            {
                var left = substitute(token.Groups["a"].Value);
                var right = substitute(token.Groups["b"].Value);
                if (Unresolved(left) || Unresolved(right))
                {
                    return null;
                }

                var equal = string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
                all &= token.Groups["op"].Value == "==" ? equal : !equal;
            }
            else if (token.Groups["or"].Success)
            {
                any |= all;
                all = true;
            }
        }

        return any || all;
    }

    /// <summary>
    /// Reports whether a value still holds a property function, item, or metadata reference after substitution.
    /// </summary>
    public static bool Unresolved(string value) =>
        value.Contains("$(", StringComparison.Ordinal)
        || value.Contains("@(", StringComparison.Ordinal)
        || value.Contains("%(", StringComparison.Ordinal);

    [GeneratedRegex(@"^\s*'[^']*'\s*(?:==|!=)\s*'[^']*'(?:\s+(?:and|or)\s+'[^']*'\s*(?:==|!=)\s*'[^']*')*\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Whole();

    [GeneratedRegex(@"(?<term>'(?<a>[^']*)'\s*(?<op>==|!=)\s*'(?<b>[^']*)')|(?<and>\band\b)|(?<or>\bor\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Token();
}
