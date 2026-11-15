namespace PandaBear.Sandbox;

/// <summary>The kind of a <see cref="SettingsChange"/>.</summary>
public enum SettingsChangeKind
{
    /// <summary>An entry is appended to a list.</summary>
    Add,

    /// <summary>An entry is removed from a list (only <c>denyRead</c>, with a risk flag).</summary>
    Remove,

    /// <summary>A boolean property is set to true.</summary>
    Set,
}
