using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Outcome of placing accepted findings.</summary>
internal sealed record PlacementResult
{
    /// <summary>Source item after placement.</summary>
    public required VaultItem Item
    {
        get; init;
    }

    /// <summary>Items created by placement, such as SSH key or card items.</summary>
    public IReadOnlyList<VaultItem> NewItems { get; init; } = [];

    /// <summary>Identifier of the finding that created each new item, aligned with <see cref="NewItems"/>.</summary>
    public IReadOnlyList<string> NewItemFindingIds { get; init; } = [];

    /// <summary>Findings that could not be placed, described without values.</summary>
    public IReadOnlyList<string> Problems { get; init; } = [];
}
