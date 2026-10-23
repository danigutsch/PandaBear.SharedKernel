using System.Text.Json.Serialization;

namespace PandaBear.VaultManager.Tool.Brief;

/// <summary>
/// A member that holds a value for a field. The value is shown only for names, folders, user names without a secret
/// finding, website addresses with their user info, query, and fragment masked, and notes that the user shared; other
/// fields show only flags.
/// </summary>
/// <param name="OriginKey">Origin key of the member.</param>
/// <param name="Newest">
/// Whether the member holds the newest value: every holder has a modification time, and the newest holder of this value is
/// newer than the newest holder of every other value, as the newest policy decides. Always false for lists, which are combined.
/// </param>
/// <param name="Value">The value, for a single value that may be shown.</param>
/// <param name="Values">The masked website addresses, for <c>login.uris</c>.</param>
/// <param name="Weak">For <c>login.password</c>, whether the password is weak.</param>
/// <param name="Reused">For <c>login.password</c>, whether an item of another group on other hosts has the same password.</param>
/// <param name="InOtherHistory">For <c>login.password</c>, whether the password is in another member's password history.</param>
internal sealed record BriefHolder(
    string OriginKey,
    bool Newest,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Value = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Values = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Weak = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Reused = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? InOtherHistory = null);
