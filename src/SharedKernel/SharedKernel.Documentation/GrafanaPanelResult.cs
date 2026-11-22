namespace PandaBear.Documentation;

/// <summary>The outcome of verifying one expected or observed panel.</summary>
public sealed class GrafanaPanelResult
{
    /// <summary>Creates a result for an observed or missing panel.</summary>
    internal GrafanaPanelResult(string title, GrafanaPanelObservation? observation, GrafanaPanelIssues issues)
    {
        Title = title;
        Observation = observation;
        Issues = issues;
    }

    /// <summary>Gets the panel title.</summary>
    public string Title { get; }

    /// <summary>Gets the rendering observation, or null for a missing panel.</summary>
    public GrafanaPanelObservation? Observation { get; }

    /// <summary>Gets the verification failures.</summary>
    public GrafanaPanelIssues Issues { get; }

    /// <summary>Gets whether the panel passed verification.</summary>
    public bool Passed => Issues == GrafanaPanelIssues.None;
}
