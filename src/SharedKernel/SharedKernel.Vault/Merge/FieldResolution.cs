namespace SharedKernel.Vault.Merge;

/// <summary>How one changed field of a group was resolved.</summary>
/// <param name="FieldPath">Field path.</param>
/// <param name="Resolution">Resolution.</param>
public sealed record FieldResolution(string FieldPath, ResolutionKind Resolution);
