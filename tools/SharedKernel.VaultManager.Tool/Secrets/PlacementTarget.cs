namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Proposed destination of a finding.</summary>
/// <param name="Kind">Destination kind.</param>
/// <param name="FieldName">Custom field name, for field destinations named by the tool.</param>
/// <param name="NameFromLabel">
/// Whether a custom field destination is named after the label found next to the value, such as a security question.
/// The label is read from the item at placement time, so it never travels with the finding.
/// </param>
/// <param name="Reason">Why the finding is not moved, for <see cref="PlacementTargetKind.NotMoved"/>.</param>
internal sealed record PlacementTarget(PlacementTargetKind Kind, string? FieldName = null, bool NameFromLabel = false, string? Reason = null)
{
    /// <summary>The login TOTP slot.</summary>
    public static PlacementTarget LoginTotp { get; } = new(PlacementTargetKind.LoginTotp);

    /// <summary>The login password slot.</summary>
    public static PlacementTarget LoginPassword { get; } = new(PlacementTargetKind.LoginPassword);

    /// <summary>The login user name slot.</summary>
    public static PlacementTarget LoginUsername { get; } = new(PlacementTargetKind.LoginUsername);

    /// <summary>The login URI list.</summary>
    public static PlacementTarget LoginUris { get; } = new(PlacementTargetKind.LoginUris);

    /// <summary>Login slots filled from a connection string, which is also kept in a hidden field.</summary>
    public static PlacementTarget LoginFromConnection { get; } = new(PlacementTargetKind.LoginFromConnection, "Connection string");

    /// <summary>A new SSH key item.</summary>
    public static PlacementTarget NewSshKeyItem { get; } = new(PlacementTargetKind.NewSshKeyItem);

    /// <summary>A new card item.</summary>
    public static PlacementTarget NewCardItem { get; } = new(PlacementTargetKind.NewCardItem);

    /// <summary>A hidden custom field with a fixed name.</summary>
    /// <param name="name">Field name.</param>
    /// <returns>The target.</returns>
    public static PlacementTarget Hidden(string name)
    {
        return new PlacementTarget(PlacementTargetKind.HiddenField, name);
    }

    /// <summary>A hidden custom field named after the label next to the value.</summary>
    /// <param name="fallbackName">Field name used when the label cannot be read.</param>
    /// <returns>The target.</returns>
    public static PlacementTarget HiddenNamedByLabel(string fallbackName)
    {
        return new PlacementTarget(PlacementTargetKind.HiddenField, fallbackName, NameFromLabel: true);
    }

    /// <summary>A text custom field with a fixed name.</summary>
    /// <param name="name">Field name.</param>
    /// <returns>The target.</returns>
    public static PlacementTarget Text(string name)
    {
        return new PlacementTarget(PlacementTargetKind.TextField, name);
    }

    /// <summary>Not moved.</summary>
    /// <param name="reason">Why the finding stays where it is.</param>
    /// <returns>The target.</returns>
    public static PlacementTarget NotMoved(string reason)
    {
        return new PlacementTarget(PlacementTargetKind.NotMoved, Reason: reason);
    }

    /// <summary>Describes the target without any value.</summary>
    /// <returns>The description.</returns>
    public override string ToString()
    {
        return Kind switch
        {
            PlacementTargetKind.LoginTotp => "login.totp",
            PlacementTargetKind.LoginPassword => "login.password",
            PlacementTargetKind.LoginUsername => "login.username",
            PlacementTargetKind.LoginUris => "login.uris",
            PlacementTargetKind.LoginFromConnection => $"login fields + hidden field '{FieldName}'",
            PlacementTargetKind.HiddenField when NameFromLabel => "hidden field named after its label",
            PlacementTargetKind.HiddenField => $"hidden field '{FieldName}'",
            PlacementTargetKind.TextField => $"text field '{FieldName}'",
            PlacementTargetKind.NewSshKeyItem => "new SSH key item",
            PlacementTargetKind.NewCardItem => "new card item",
            _ => $"not moved ({Reason})",
        };
    }
}
