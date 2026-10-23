namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Where an accepted finding is moved to.</summary>
internal enum PlacementTargetKind
{
    /// <summary>The login TOTP slot.</summary>
    LoginTotp = 0,

    /// <summary>The login password slot.</summary>
    LoginPassword,

    /// <summary>The login user name slot.</summary>
    LoginUsername,

    /// <summary>The login URI list.</summary>
    LoginUris,

    /// <summary>Empty login user name, password, and URI slots, plus a hidden field holding the full string.</summary>
    LoginFromConnection,

    /// <summary>A hidden custom field.</summary>
    HiddenField,

    /// <summary>A text custom field.</summary>
    TextField,

    /// <summary>A new SSH key item.</summary>
    NewSshKeyItem,

    /// <summary>A new card item.</summary>
    NewCardItem,

    /// <summary>Left where it is.</summary>
    NotMoved,
}
