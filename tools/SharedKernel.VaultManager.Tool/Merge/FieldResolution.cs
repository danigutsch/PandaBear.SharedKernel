namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>How one changed field of a group was resolved.</summary>
/// <param name="FieldPath">Field path.</param>
/// <param name="Resolution">Resolution.</param>
internal sealed record FieldResolution(string FieldPath, ResolutionKind Resolution);
