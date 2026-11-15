namespace PandaBear.Sandbox;

/// <summary>One edit that a settings plan would make, or one managed entry that drifted.</summary>
/// <param name="Kind">What happens to the entry.</param>
/// <param name="Location">
/// Where the entry is in the settings file, in the file format's own notation, such as
/// <c>$.sandbox.filesystem.allowWrite</c> for a JSON file.
/// </param>
/// <param name="Value">The configured entry, or the variable name for an environment variable. Never a value of the user's own file.</param>
public sealed record SettingsChange(SettingsChangeKind Kind, string Location, string Value);
