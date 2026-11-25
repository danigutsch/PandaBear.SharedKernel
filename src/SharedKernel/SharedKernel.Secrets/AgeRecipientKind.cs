namespace SharedKernel.Secrets;

/// <summary>The kind of an <see cref="AgeRecipient"/>.</summary>
public enum AgeRecipientKind
{
    /// <summary>A native X25519 recipient (<c>age1...</c>) whose payload is exactly 32 bytes.</summary>
    X25519,

    /// <summary>A native post-quantum hybrid recipient (<c>age1pq1...</c>).</summary>
    Hybrid,

    /// <summary>A native tagged recipient (<c>age1tag1...</c> or <c>age1tagpq1...</c>).</summary>
    Tag,

    /// <summary>A plugin recipient on the allow-list (<c>age1yubikey1...</c>, <c>age1tpm1...</c>, <c>age1se1...</c>, <c>age1fido2-hmac1...</c>).</summary>
    Plugin,

    /// <summary>An SSH recipient line: <c>ssh-ed25519 &lt;base64&gt;</c> or <c>ssh-rsa &lt;base64&gt;</c>, without a comment.</summary>
    Ssh,
}
