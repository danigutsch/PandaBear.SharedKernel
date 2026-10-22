using PandaBear.VaultManager.Tool.Matching;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>
/// On-disk shape of the decisions file. Holds group keys, field paths, origin keys, and choices, never values. A later
/// version can add sections, such as accepted secret findings, as new properties.
/// </summary>
/// <param name="Version">Format version; <see cref="DecisionsFile.CurrentVersion"/>.</param>
/// <param name="Groups">Field decisions per group.</param>
/// <param name="Links">Forced links.</param>
/// <param name="Splits">Forced splits.</param>
internal sealed record DecisionsDocument(int Version, IReadOnlyList<GroupDecision> Groups, IReadOnlyList<OriginKeyPair> Links, IReadOnlyList<OriginKeyPair> Splits);
