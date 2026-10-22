using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>Single-valued text field of an item, with how to read, write, and compare it.</summary>
/// <param name="Path">Field path.</param>
/// <param name="Label">Human label used in names of preserved fields, for example <c>Password</c>.</param>
/// <param name="Owner">Item type the field belongs to, or <see langword="null"/> when every type has it.</param>
/// <param name="Get">Reads the value.</param>
/// <param name="Set">Returns a copy of an item with the value replaced.</param>
/// <param name="Key">Comparison key of a present value.</param>
internal sealed record ScalarField(
    string Path,
    string Label,
    VaultItemType? Owner,
    Func<VaultItem, string?> Get,
    Func<VaultItem, string?, VaultItem> Set,
    Func<string, string> Key)
{
    /// <summary>Whether an item of the given type has this field.</summary>
    /// <param name="type">Item type.</param>
    /// <returns><see langword="true"/> when the field applies.</returns>
    public bool AppliesTo(VaultItemType type) => Owner is null || Owner == type;
}
