namespace PandaBear.VaultManager.Tool.Model;

/// <summary>Autofill URI match detection, numbered like Bitwarden.</summary>
internal enum UriMatch
{
    /// <summary>Same base domain.</summary>
    Domain = 0,

    /// <summary>Same host and port.</summary>
    Host = 1,

    /// <summary>URL starts with the value.</summary>
    StartsWith = 2,

    /// <summary>Exact URL.</summary>
    Exact = 3,

    /// <summary>Regular expression.</summary>
    RegularExpression = 4,

    /// <summary>Never autofill.</summary>
    Never = 5,
}
