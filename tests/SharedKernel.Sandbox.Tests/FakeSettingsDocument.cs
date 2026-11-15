namespace PandaBear.Sandbox.Tests;

/// <summary>An in-memory settings document: lists, a flag, and environment variables, with locations named after the setting.</summary>
internal sealed class FakeSettingsDocument : ISandboxSettingsDocument
{
    private readonly Dictionary<SandboxSetting, List<string>> _lists = [];
    private readonly Dictionary<string, string?> _environment = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the flag value; <see langword="null"/> stands for a value that is not a boolean.</summary>
    public bool? Flag { get; set; } = false;

    /// <summary>Gets or sets the text the document was opened from.</summary>
    public string? Text { get; set; }

    /// <summary>Gets how often the document was serialized.</summary>
    public int Serializations { get; private set; }

    /// <inheritdoc />
    public string Name => "fake.json";

    /// <summary>Adds entries to a list as if the file held them.</summary>
    public FakeSettingsDocument With(SandboxSetting setting, params string[] entries)
    {
        List(setting).AddRange(entries);
        return this;
    }

    /// <summary>Sets an environment variable as if the file held it; <see langword="null"/> stands for a value that is not text.</summary>
    public FakeSettingsDocument WithVariable(string name, string? value)
    {
        _environment[name] = value;
        return this;
    }

    /// <summary>Returns the current entries of a list.</summary>
    public IReadOnlyList<string> Entries(SandboxSetting setting) => List(setting);

    /// <summary>Returns the current environment variables.</summary>
    public IReadOnlyDictionary<string, string?> Variables => _environment;

    /// <inheritdoc />
    public string LocationOf(SandboxSetting setting) => "@" + setting;

    /// <inheritdoc />
    public IReadOnlyList<string> GetEntries(SandboxSetting setting) => [.. List(setting)];

    /// <inheritdoc />
    public void AddEntry(SandboxSetting setting, string entry) => List(setting).Add(entry);

    /// <inheritdoc />
    public void RemoveEntry(SandboxSetting setting, int index) => List(setting).RemoveAt(index);

    /// <inheritdoc />
    public bool? GetFlag(SandboxSetting setting) => Flag;

    /// <inheritdoc />
    public void SetFlag(SandboxSetting setting) => Flag = true;

    /// <inheritdoc />
    public bool TryGetEnvironmentVariable(string name, out string? value) => _environment.TryGetValue(name, out value);

    /// <inheritdoc />
    public void SetEnvironmentVariable(string name, string value) => _environment[name] = value;

    /// <inheritdoc />
    public string Serialize()
    {
        Serializations++;
        return "serialized";
    }

    private List<string> List(SandboxSetting setting)
    {
        if (!_lists.TryGetValue(setting, out var list))
        {
            list = [];
            _lists[setting] = list;
        }

        return list;
    }
}
