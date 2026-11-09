namespace PandaBear.Sandbox.Tool;

/// <summary>One edit that the settings command would make, or one entry that drifted.</summary>
/// <param name="Kind">What happens to the entry.</param>
/// <param name="JsonPath">The JSON path of the list or property, such as <c>$.sandbox.filesystem.allowWrite</c>.</param>
/// <param name="Value">The configured entry, or the property name for an environment variable. Never a value of the user's own file.</param>
internal sealed record SettingsChange(SettingsChangeKind Kind, string JsonPath, string Value);
