using PandaBear.VaultManager.Tool.Matching;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>
/// On-disk shape of the decisions file. Holds group keys, field paths, origin keys, finding identifiers, and choices,
/// never values.
/// </summary>
/// <param name="Version">Format version; <see cref="DecisionsFile.CurrentVersion"/>.</param>
/// <param name="Groups">Field decisions per group.</param>
/// <param name="Links">Forced links.</param>
/// <param name="Splits">Forced splits.</param>
/// <param name="Findings">Decisions on secret findings per input item; files without this section have none.</param>
internal sealed record DecisionsDocument(
    int Version,
    IReadOnlyList<GroupDecision> Groups,
    IReadOnlyList<OriginKeyPair> Links,
    IReadOnlyList<OriginKeyPair> Splits,
    IReadOnlyList<ItemFindingDecisions>? Findings = null);
