namespace PandaBear.Documentation;

/// <summary>The caller's observation of executing a datasource query.</summary>
/// <param name="Succeeded">Whether the datasource accepted and executed the query.</param>
/// <param name="SeriesCount">The number of results returned by the datasource.</param>
public sealed record GrafanaQueryObservation(bool Succeeded, int SeriesCount)
{
    /// <summary>Evaluates datasource success and, if required, the presence of results.</summary>
    /// <param name="requireData">Whether the caller's policy requires results.</param>
    /// <returns>Whether the query passed verification.</returns>
    public bool Passes(bool requireData) => Succeeded && (SeriesCount > 0 || !requireData);
}
