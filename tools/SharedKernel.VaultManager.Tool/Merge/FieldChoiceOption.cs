namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>A member a pending field decision can choose. It names the member, never its value.</summary>
/// <param name="OriginKey">Origin key of the member, the value a <see cref="FieldChoiceKind.Member"/> decision names.</param>
/// <param name="ItemName">Name of the member item.</param>
internal sealed record FieldChoiceOption(string OriginKey, string ItemName);
