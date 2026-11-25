using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>Maps an ordered list of plain values to one JSON text column.</summary>
/// <remarks>
/// Use it for lists that are read and written with their owner and never queried; a value that needs an index or a filter belongs in a
/// column of its own. Entity Framework Core's own JSON mapping (primitive collections, or <c>ComplexCollection(...).ToJson()</c>) is the
/// first choice for a new model; this conversion keeps one fixed, provider-independent text shape instead, so data stored by an existing
/// conversion stays loadable and the model needs no migration.
/// </remarks>
public static class JsonColumnPropertyBuilderExtensions
{
    /// <summary>
    /// Maps a list property to a required column holding the list as JSON text, serialized with <see cref="JsonSerializerOptions.Web"/>
    /// (camel-case property names, the default encoder). Lists are compared by their JSON, so assigning an equal list is not a change and a
    /// change made in place to the loaded list is detected; the snapshot is a copy of the list.
    /// </summary>
    /// <typeparam name="T">The item type: a string, number, id or small record that <see cref="JsonSerializer"/> round-trips.</typeparam>
    /// <param name="property">The property builder.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is <see langword="null"/>.</exception>
    [RequiresUnreferencedCode("Reflection-based JSON serialization of the item type is not trimming compatible.")]
    [RequiresDynamicCode("Reflection-based JSON serialization of the item type is not Native AOT compatible.")]
    public static PropertyBuilder<IReadOnlyList<T>> HasJsonConversion<T>(this PropertyBuilder<IReadOnlyList<T>> property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return property
            .HasConversion(
                list => JsonSerializer.Serialize(list, JsonSerializerOptions.Web),
                text => JsonSerializer.Deserialize<List<T>>(text, JsonSerializerOptions.Web) ?? new List<T>(),
                new ValueComparer<IReadOnlyList<T>>(
                    (left, right) => JsonSerializer.Serialize(left, JsonSerializerOptions.Web) == JsonSerializer.Serialize(right, JsonSerializerOptions.Web),
                    list => JsonSerializer.Serialize(list, JsonSerializerOptions.Web).GetHashCode(StringComparison.Ordinal),
                    list => list.ToList()))
            .IsRequired();
    }
}
