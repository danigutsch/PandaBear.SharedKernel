namespace PandaBear.VaultManager.Tool.Review;

/// <summary>A member of a match group as the review names it. Holds no values.</summary>
/// <param name="OriginKey">Origin key of the member, which member choices, links, and splits name.</param>
/// <param name="Name">Item name.</param>
/// <param name="SourceFile">Name of the input file the item was read from.</param>
/// <param name="LastModified">Latest known modification time, or <see langword="null"/> when the source reports none.</param>
/// <param name="IsNewest">Whether this member alone has the latest modification time of the group.</param>
internal sealed record ReviewMember(string OriginKey, string Name, string SourceFile, DateTimeOffset? LastModified, bool IsNewest);
