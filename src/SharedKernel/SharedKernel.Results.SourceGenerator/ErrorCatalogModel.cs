using System.Collections.Immutable;

namespace SharedKernel.Results.SourceGenerator;

internal sealed class ErrorCatalogModel(ImmutableArray<ErrorCatalogEntryModel> entries) : IEquatable<ErrorCatalogModel>
{
    public ImmutableArray<ErrorCatalogEntryModel> Entries { get; } = entries;

    public bool Equals(ErrorCatalogModel? other)
    {
        return other is not null && Entries.SequenceEqual(other.Entries);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ErrorCatalogModel);
    }

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var entry in Entries)
        {
            hash = (hash * 31) + entry.GetHashCode();
        }

        return hash;
    }
}
