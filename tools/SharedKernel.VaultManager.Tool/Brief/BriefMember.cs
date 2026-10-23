namespace PandaBear.VaultManager.Tool.Brief;

/// <summary>A member of a brief group: an input item as the merge sees it, after accepted findings are placed.</summary>
/// <param name="OriginKey">Origin key, which member choices and finding decisions name.</param>
/// <param name="SourceFile">File name of the input the item was read from.</param>
/// <param name="Name">Item name.</param>
/// <param name="Folder">Folder path, or <see langword="null"/> for none.</param>
/// <param name="Host">Host of the item's first website address with a host, or <see langword="null"/>.</param>
/// <param name="Username">User name of a login, or <see langword="null"/> when it has none or a secret finding lies in it.</param>
/// <param name="Modified">Modification time, or <see langword="null"/> when the input gives none.</param>
/// <param name="Newest">Whether every member has a modification time and this member alone has the latest.</param>
/// <param name="Findings">Secret findings of the item as read, without their values.</param>
internal sealed record BriefMember(
    string OriginKey,
    string SourceFile,
    string Name,
    string? Folder,
    string? Host,
    string? Username,
    DateTimeOffset? Modified,
    bool Newest,
    IReadOnlyList<BriefFinding> Findings);
