namespace PandaBear.Sandbox;

/// <summary>The result of comparing the desired entries with a settings file.</summary>
/// <param name="Changes">The edits needed; empty when the file is already in line.</param>
/// <param name="Warnings">Things worth knowing that are not drift, such as a conflicting environment value.</param>
/// <param name="Content">The new file content; when <paramref name="Changes"/> is empty, the current content, or an empty string when the file does not exist.</param>
/// <param name="ExpectedContent">The current content, or <see langword="null"/> when the file does not exist.</param>
public sealed record SettingsPlan(IReadOnlyList<SettingsChange> Changes, IReadOnlyList<string> Warnings, string Content, string? ExpectedContent);
