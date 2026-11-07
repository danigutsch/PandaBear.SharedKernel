using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PandaBear.EntityFrameworkCore;

/// <summary>Names the application rule that a unique index enforces.</summary>
public static class UniqueRuleIndexBuilderExtensions
{
    /// <summary>The model annotation that stores the rule name on an index.</summary>
    public const string AnnotationName = "PandaBear:UniqueRule";

    /// <summary>Marks a unique index with the application rule it enforces, so violations can be reported by rule.</summary>
    /// <param name="builder">The index builder.</param>
    /// <param name="rule">The application-defined rule name, for example <c>customer.email</c>.</param>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="rule"/> is null, empty or whitespace.</exception>
    public static IndexBuilder<TEntity> HasUniqueRule<TEntity>(this IndexBuilder<TEntity> builder, string rule)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);

        builder.IsUnique().HasAnnotation(AnnotationName, rule);
        return builder;
    }
}
