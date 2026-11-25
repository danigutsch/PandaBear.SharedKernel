namespace SharedKernel.ContentRights;

/// <summary>Why a person or organization is named in the public credit.</summary>
public enum ContentCreditRole
{
    /// <summary>The creator of the credited work.</summary>
    Creator,

    /// <summary>The copyright owner identified by the source.</summary>
    CopyrightOwner,

    /// <summary>The party identified as providing the terms or permission.</summary>
    Licensor,

    /// <summary>Another party designated for credit.</summary>
    Designated,
}
