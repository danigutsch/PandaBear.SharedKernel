using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Brief;

/// <summary>A group of matched items that needs a decision.</summary>
/// <param name="Key">Group key, which field decisions name.</param>
/// <param name="Type">Type of the merged item, which decides the fields it has.</param>
/// <param name="Members">Members in group order.</param>
/// <param name="Fields">Fields on which the members differ, in report order. A group with one member has none.</param>
internal sealed record BriefGroup(string Key, VaultItemType Type, IReadOnlyList<BriefMember> Members, IReadOnlyList<BriefField> Fields);
