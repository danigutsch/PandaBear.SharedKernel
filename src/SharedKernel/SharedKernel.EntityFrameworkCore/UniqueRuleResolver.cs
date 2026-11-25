using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>Finds the rule of the unique index that a provider reported as violated.</summary>
internal static class UniqueRuleResolver
{
    /// <summary>Matches a violation to a rule-tagged unique index of the model.</summary>
    /// <param name="model">The model.</param>
    /// <param name="violation">The provider's report.</param>
    /// <returns>The rule name, or <see langword="null"/> when no tagged index matches.</returns>
    internal static string? Resolve(IModel model, UniqueViolation violation)
    {
        foreach (var table in model.GetRelationalModel().Tables)
        {
            foreach (var index in table.Indexes)
            {
                if (!index.IsUnique || !Matches(index, violation))
                {
                    continue;
                }

                foreach (var mappedIndex in index.MappedIndexes)
                {
                    if (mappedIndex.FindAnnotation(UniqueRuleIndexBuilderExtensions.AnnotationName)?.Value is string rule)
                    {
                        return rule;
                    }
                }
            }
        }

        return null;
    }

    private static bool Matches(ITableIndex index, UniqueViolation violation)
    {
        if (violation.ConstraintName is { } name && string.Equals(index.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (violation.Table is not { } violationTable || violation.Columns.Count == 0
            || !string.Equals(index.Table.Name, violationTable, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return index.Columns.Count == violation.Columns.Count
            && index.Columns.All(column => violation.Columns.Contains(column.Name, StringComparer.OrdinalIgnoreCase));
    }
}
