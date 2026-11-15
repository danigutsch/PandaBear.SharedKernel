namespace PandaBear.Sandbox;

/// <summary>
/// An agent settings file in its own format, opened for <see cref="SettingsMerger"/>. The merger decides what to add,
/// remove, and warn about; the document maps each <see cref="SandboxSetting"/> to the format's keys, checks their types,
/// and writes the edited text back. A format library, such as the Claude Code one, implements it.
/// </summary>
public interface ISandboxSettingsDocument
{
    /// <summary>Gets the file name used in messages.</summary>
    string Name { get; }

    /// <summary>Gets the text the document was opened from, or <see langword="null"/> when the file does not exist.</summary>
    string? Text { get; }

    /// <summary>Returns where <paramref name="setting"/> is in the file, in the format's own notation, such as <c>$.sandbox.filesystem.allowWrite</c>.</summary>
    /// <param name="setting">The setting.</param>
    string LocationOf(SandboxSetting setting);

    /// <summary>Returns the entries of a list setting, in file order; empty when the list is absent.</summary>
    /// <param name="setting">A list setting.</param>
    /// <exception cref="InvalidOperationException">The list, or an object that holds it, has the wrong type. The message names the file and the location.</exception>
    IReadOnlyList<string> GetEntries(SandboxSetting setting);

    /// <summary>Appends <paramref name="entry"/> to a list setting, creating the list when it is absent.</summary>
    /// <param name="setting">A list setting.</param>
    /// <param name="entry">The entry to append.</param>
    void AddEntry(SandboxSetting setting, string entry);

    /// <summary>Removes the entry at <paramref name="index"/>, as <see cref="GetEntries"/> numbers them, from a list setting.</summary>
    /// <param name="setting">A list setting.</param>
    /// <param name="index">The position of the entry.</param>
    void RemoveEntry(SandboxSetting setting, int index);

    /// <summary>Returns the value of a flag setting: <see langword="false"/> when it is absent, and <see langword="null"/> when it has a value that is not a boolean.</summary>
    /// <param name="setting">A flag setting.</param>
    /// <exception cref="InvalidOperationException">An object that holds the flag has the wrong type. The message names the file and the location.</exception>
    bool? GetFlag(SandboxSetting setting);

    /// <summary>Sets a flag setting to <see langword="true"/>, creating what holds it when it is absent.</summary>
    /// <param name="setting">A flag setting.</param>
    void SetFlag(SandboxSetting setting);

    /// <summary>Looks up an environment variable that the file sets.</summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The value, or <see langword="null"/> when the file gives a value that is not text.</param>
    /// <returns>Whether the file gives the variable a value.</returns>
    /// <exception cref="InvalidOperationException">The environment section has the wrong type. The message names the file and the location.</exception>
    bool TryGetEnvironmentVariable(string name, out string? value);

    /// <summary>Sets an environment variable, creating the environment section when it is absent.</summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The value.</param>
    void SetEnvironmentVariable(string name, string value);

    /// <summary>Writes the edited document as text in the file's own layout.</summary>
    /// <exception cref="InvalidOperationException">The document holds something that cannot be written back. The message names the file.</exception>
    string Serialize();
}
