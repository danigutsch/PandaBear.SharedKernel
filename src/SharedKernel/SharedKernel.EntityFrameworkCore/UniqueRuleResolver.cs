using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace PandaBear.EntityFrameworkCore;

/// <summary>Finds the rule of the unique index that a provider reported as violated.</summary>
internal static class UniqueRuleResolver
{
    /// <summary>Matches a violation to a rule-tagged unique index of the model.</summary>
    /// <param name="model">The model.</param>
    /// <param name="violation">The provider's report.</param>
    /// <returns>The rule name, or <see langword="null"/> when no tagged index matches.</returns>
    internal static string? Resolve(IModel model, UniqueViolation violation)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            var table = entityType.GetTableName();
            if (table is null)
            {
                continue;
            }

            var store = StoreObjectIdentifier.Table(table, entityType.GetSchema());
            foreach (var index in entityType.GetDeclaredIndexes())
            {
                if (!index.IsUnique || index.FindAnnotation(UniqueRuleIndexBuilderExtensions.AnnotationName)?.Value is not string rule)
                {
                    continue;
                }

                if (Matches(index, table, store, violation))
                {
                    return rule;
                }
            }
        }

        return null;
    }

    private static bool Matches(IIndex index, string table, StoreObjectIdentifier store, UniqueViolation violation)
    {
        if (violation.ConstraintName is { } name && string.Equals(index.GetDatabaseName(store), name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (violation.Table is not { } violationTable || violation.Columns.Count == 0
            || !string.Equals(table, violationTable, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var columns = index.Properties.Select(property => property.GetColumnName(store)).ToList();
        return columns.Count == violation.Columns.Count
            && columns.All(column => violation.Columns.Contains(column, StringComparer.OrdinalIgnoreCase));
    }
}
