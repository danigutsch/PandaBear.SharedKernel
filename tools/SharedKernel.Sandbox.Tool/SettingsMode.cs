namespace PandaBear.Sandbox.Tool;

/// <summary>What the settings command does with the plan.</summary>
internal enum SettingsMode
{
    /// <summary>Print the plan; never write.</summary>
    Plan,

    /// <summary>Print the plan and write it when forced.</summary>
    Apply,

    /// <summary>Report drift of the managed entries.</summary>
    Verify,
}
