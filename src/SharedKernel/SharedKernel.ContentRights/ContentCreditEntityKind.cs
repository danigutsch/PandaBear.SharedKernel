namespace PandaBear.ContentRights;

/// <summary>The recorded kind of the credited party.</summary>
public enum ContentCreditEntityKind
{
    /// <summary>The caller has not established whether the party is a person or organization.</summary>
    Unknown,

    /// <summary>The party is an individual.</summary>
    Person,

    /// <summary>The party is an organization.</summary>
    Organization,
}
