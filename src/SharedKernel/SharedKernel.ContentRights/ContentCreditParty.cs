namespace PandaBear.ContentRights;

/// <summary>A party explicitly named in the public credit.</summary>
/// <param name="Name">The party's public name as supplied.</param>
/// <param name="Role">The reason for crediting the party.</param>
/// <param name="EntityKind">The known kind of party, if supplied.</param>
public sealed record ContentCreditParty(
    string Name,
    ContentCreditRole Role,
    ContentCreditEntityKind EntityKind = ContentCreditEntityKind.Unknown);
