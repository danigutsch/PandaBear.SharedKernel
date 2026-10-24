namespace PandaBear.CI.Tool;

/// <summary>The symbol evidence that <c>coverage record-symbols</c> writes and <c>coverage verify</c> checks.</summary>
/// <param name="Revision">The Git commit that the build compiled.</param>
/// <param name="Files">The SHA-256 hash, as lowercase hexadecimal, of each Release portable PDB, by repository-relative path.</param>
internal sealed record SymbolEvidenceFile(string Revision, Dictionary<string, string> Files);
