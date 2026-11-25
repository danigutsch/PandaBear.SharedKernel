using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SharedKernel.Style.Analyzers;

/// <summary>Parses the catalog shared by the literal analyzer and the constant guard.</summary>
internal static class GovernedLiteralCatalog
{
    /// <summary>Reads canonical declaration and compatibility exception rows.</summary>
    /// <param name="text">The catalog text.</param>
    /// <returns>The parsed catalog rows.</returns>
    /// <exception cref="FormatException">A row or the set of owners and exceptions is invalid.</exception>
    public static ImmutableArray<GovernedLiteral> Parse(string text)
    {
        var entries = ImmutableArray.CreateBuilder<GovernedLiteral>();
        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var row = line.Trim();
            if (row.Length == 0 || row[0] == '#')
            {
                continue;
            }

            var cells = row.Split('|').Select(static cell => cell.Trim()).ToArray();
            if (cells.Length is not (3 or 4) || cells.Any(string.IsNullOrWhiteSpace)
                || !cells[2].Split('.').All(SyntaxFacts.IsValidIdentifier))
            {
                throw new FormatException("Expected value|assembly|fully.qualified.Field, or an exception with a fourth justification column.");
            }

            entries.Add(new GovernedLiteral(cells[0], cells[1], cells[2], cells.Length == 4 ? cells[3] : null));
        }

        var result = entries.ToImmutable();
        if (result.Where(static entry => entry.Justification is null).GroupBy(static entry => entry.Value, StringComparer.Ordinal).Any(static group => group.Count() != 1)
            || result.GroupBy(static entry => (entry.Assembly, entry.Symbol)).Any(static group => group.Count() != 1)
            || result.Any(entry => entry.Justification is not null && !result.Any(owner => owner.Justification is null && owner.Value == entry.Value)))
        {
            throw new FormatException("Each value requires one canonical owner; symbols must be unique and exceptions must name a registered value.");
        }

        return result;
    }

    /// <summary>Tests whether the declared field matches the catalogued assembly and fully qualified name.</summary>
    /// <param name="entry">The catalog row.</param>
    /// <param name="field">The declared field.</param>
    /// <returns>True when the declaration matches the catalog row.</returns>
    public static bool Matches(GovernedLiteral entry, IFieldSymbol field) =>
        string.Equals(entry.Assembly, field.ContainingAssembly.Name, StringComparison.Ordinal)
        && string.Equals(entry.Symbol, field.ToDisplayString(), StringComparison.Ordinal);
}
