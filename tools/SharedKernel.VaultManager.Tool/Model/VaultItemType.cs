using System.Diagnostics.CodeAnalysis;

namespace PandaBear.VaultManager.Tool.Model;

/// <summary>Vault item kinds, numbered like Bitwarden cipher types 1 to 5.</summary>
[SuppressMessage("Design", "CA1008:Enums should have zero value", Justification = "Values mirror Bitwarden cipher types, which start at 1.")]
internal enum VaultItemType
{
    /// <summary>Website or service credentials.</summary>
    Login = 1,

    /// <summary>Free-form note.</summary>
    SecureNote = 2,

    /// <summary>Payment card.</summary>
    Card = 3,

    /// <summary>Personal identity data.</summary>
    Identity = 4,

    /// <summary>SSH key pair.</summary>
    SshKey = 5,
}
