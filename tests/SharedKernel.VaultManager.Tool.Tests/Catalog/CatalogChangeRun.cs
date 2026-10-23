using PandaBear.VaultManager.Tool.Catalog;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Catalog;

/// <summary>Applies an edit of the single catalog entry of a one-item vault, as an agent edits a catalog.</summary>
internal static class CatalogChangeRun
{
    /// <summary>Line prefix that names the fixture login by its key and name as read.</summary>
    public const string Prefix = CatalogFixture.Key + " (\"GitHub\"): ";

    /// <summary>Builds the catalog of the item, edits its entry, and applies the catalog.</summary>
    public static CatalogApplyResult Apply(VaultItem item, Func<CatalogItem, CatalogItem> edit, MergeDecisions? decisions = null, IReadOnlyList<string>? folders = null)
    {
        decisions ??= MergeDecisions.None;
        var vault = CatalogFixture.Vault(item);
        CatalogItem entry = CatalogBuilder.Build(vault, "abc", decisions).Catalog.Items[0];
        CatalogApplyResult result = CatalogApplier.Apply(vault, CatalogFixture.CatalogWith(vault, [edit(entry)], folders), decisions);
        result.Problems.ShouldBeEmpty();
        return result;
    }
}
