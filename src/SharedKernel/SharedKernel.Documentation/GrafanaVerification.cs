// Adapted from ViajantesTurismo. See NOTICE.ViajantesTurismo.txt for the MIT license and attribution.

namespace PandaBear.Documentation;

/// <summary>Verifies Grafana dashboard and datasource observations supplied by a host.</summary>
public static class GrafanaVerification
{
    /// <summary>Checks rendered panels, requiring data for the supplied titles.</summary>
    /// <param name="requiredDataTitles">The panel titles that must be present with data.</param>
    /// <param name="observations">The panels observed by the caller, in presentation order.</param>
    /// <returns>Outcomes in observation order, followed by missing required titles in caller order.</returns>
    /// <exception cref="ArgumentNullException">Either collection or one of its observations is null.</exception>
    public static IReadOnlyList<GrafanaPanelResult> CheckPanels(
        IEnumerable<string> requiredDataTitles, IEnumerable<GrafanaPanelObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(requiredDataTitles);
        ArgumentNullException.ThrowIfNull(observations);

        var expected = requiredDataTitles.Distinct(StringComparer.Ordinal).ToArray();
        var requiresData = expected.ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<GrafanaPanelResult>();
        foreach (var observation in observations)
        {
            ArgumentNullException.ThrowIfNull(observation);
            seen.Add(observation.Title);
            var issues = GrafanaPanelIssues.None;
            if (!observation.Drawn)
            {
                issues |= GrafanaPanelIssues.NotDrawn;
            }

            if (observation.QueryError)
            {
                issues |= GrafanaPanelIssues.QueryError;
            }

            if (observation.NoData && requiresData.Contains(observation.Title))
            {
                issues |= GrafanaPanelIssues.NoData;
            }

            results.Add(new GrafanaPanelResult(observation.Title, observation, issues));
        }

        results.AddRange(expected.Where(title => !seen.Contains(title))
            .Select(title => new GrafanaPanelResult(title, observation: null, GrafanaPanelIssues.Missing)));
        return results.AsReadOnly();
    }

    /// <summary>Compares expected and observed file-provisioned targets in their original order.</summary>
    /// <param name="provisioned">Whether the observed dashboard was confirmed as file-provisioned.</param>
    /// <param name="expected">The caller's configured target sequence.</param>
    /// <param name="observed">The target sequence read from the live dashboard.</param>
    /// <returns>The provisioning comparison outcome.</returns>
    /// <exception cref="ArgumentNullException">Either target sequence is null.</exception>
    public static GrafanaProvisioningStatus CheckProvisioning(
        bool provisioned, IEnumerable<GrafanaQueryTarget> expected, IEnumerable<GrafanaQueryTarget> observed)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(observed);

        if (!provisioned)
        {
            return GrafanaProvisioningStatus.NotProvisioned;
        }

        return expected.SequenceEqual(observed) ? GrafanaProvisioningStatus.Current : GrafanaProvisioningStatus.TargetsDiffer;
    }
}
