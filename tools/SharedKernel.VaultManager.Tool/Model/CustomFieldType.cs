namespace PandaBear.VaultManager.Tool.Model;

/// <summary>Custom field kinds, numbered like Bitwarden field types.</summary>
internal enum CustomFieldType
{
    /// <summary>Visible text.</summary>
    Text = 0,

    /// <summary>Masked text.</summary>
    Hidden = 1,

    /// <summary>Checkbox value stored as <c>true</c> or <c>false</c>.</summary>
    Boolean = 2,

    /// <summary>Reference to another item property.</summary>
    Linked = 3,
}
