using System.Text.Json;

namespace PandaBear.Packages;

/// <summary>
/// Maps the severity of an OSV record to <see cref="AdvisorySeverity"/>. In order: the database's own label in
/// <c>database_specific.severity</c> (<c>CRITICAL</c>, <c>HIGH</c>, <c>MODERATE</c> or <c>MEDIUM</c>, <c>LOW</c>), then the
/// base score of a <c>CVSS_V3</c> vector, which this class computes. CVSS v2, CVSS v4, Ubuntu priorities, and a missing or
/// unreadable severity give <see cref="Unknown"/>, which is <see cref="AdvisorySeverity.High"/>: not knowing never
/// understates an advisory.
/// </summary>
internal static class OsvSeverity
{
    /// <summary>The severity used when the record does not give one that this class can read.</summary>
    public const AdvisorySeverity Unknown = AdvisorySeverity.High;

    /// <summary>Maps a record to a severity; see the class summary for the order.</summary>
    public static AdvisorySeverity Map(OsvWire.Vulnerability vulnerability)
    {
        if (vulnerability.DatabaseSpecific?.Severity is { ValueKind: JsonValueKind.String } label && FromLabel(label.GetString()) is { } labelled)
        {
            return labelled;
        }

        foreach (var entry in vulnerability.Severity ?? [])
        {
            if (entry.Type == "CVSS_V3" && BaseScore(entry.Score) is { } score)
            {
                return FromScore(score);
            }
        }

        return Unknown;
    }

    /// <summary>Maps a database label, or returns <see langword="null"/> when it is not a known label.</summary>
    public static AdvisorySeverity? FromLabel(string? label) => label switch
    {
        null => null,
        _ when label.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase) => AdvisorySeverity.Critical,
        _ when label.Equals("HIGH", StringComparison.OrdinalIgnoreCase) => AdvisorySeverity.High,
        _ when label.Equals("MODERATE", StringComparison.OrdinalIgnoreCase) || label.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase) => AdvisorySeverity.Medium,
        _ when label.Equals("LOW", StringComparison.OrdinalIgnoreCase) => AdvisorySeverity.Low,
        _ => null,
    };

    /// <summary>Maps a CVSS base score by the standard bands; 0 to 3.9 is low.</summary>
    public static AdvisorySeverity FromScore(double score) => score switch
    {
        >= 9.0 => AdvisorySeverity.Critical,
        >= 7.0 => AdvisorySeverity.High,
        >= 4.0 => AdvisorySeverity.Medium,
        _ => AdvisorySeverity.Low,
    };

    /// <summary>Computes the CVSS v3.x base score of a vector such as <c>CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H</c>, or returns <see langword="null"/>.</summary>
    public static double? BaseScore(string? vector)
    {
        if (vector is null || !(vector.StartsWith("CVSS:3.0/", StringComparison.Ordinal) || vector.StartsWith("CVSS:3.1/", StringComparison.Ordinal)))
        {
            return null;
        }

        Dictionary<string, string> metrics = [];
        foreach (var part in vector.Split('/').Skip(1))
        {
            var pair = part.Split(':');
            if (pair.Length != 2 || !metrics.TryAdd(pair[0], pair[1]))
            {
                return null;
            }
        }

        if (!metrics.TryGetValue("S", out var scope) || scope is not ("U" or "C"))
        {
            return null;
        }

        var changed = scope == "C";
        var attackVector = Weight(metrics, "AV", ("N", 0.85), ("A", 0.62), ("L", 0.55), ("P", 0.2));
        var complexity = Weight(metrics, "AC", ("L", 0.77), ("H", 0.44));
        var privileges = changed
            ? Weight(metrics, "PR", ("N", 0.85), ("L", 0.68), ("H", 0.5))
            : Weight(metrics, "PR", ("N", 0.85), ("L", 0.62), ("H", 0.27));
        var interaction = Weight(metrics, "UI", ("N", 0.85), ("R", 0.62));
        var confidentiality = Weight(metrics, "C", ("H", 0.56), ("L", 0.22), ("N", 0));
        var integrity = Weight(metrics, "I", ("H", 0.56), ("L", 0.22), ("N", 0));
        var availability = Weight(metrics, "A", ("H", 0.56), ("L", 0.22), ("N", 0));
        if (attackVector is null || complexity is null || privileges is null || interaction is null || confidentiality is null || integrity is null || availability is null)
        {
            return null;
        }

        var iss = 1 - ((1 - confidentiality.Value) * (1 - integrity.Value) * (1 - availability.Value));
        var impact = changed ? (7.52 * (iss - 0.029)) - (3.25 * Math.Pow(iss - 0.02, 15)) : 6.42 * iss;
        if (impact <= 0)
        {
            return 0;
        }

        var exploitability = 8.22 * attackVector.Value * complexity.Value * privileges.Value * interaction.Value;
        return RoundUp(Math.Min((changed ? 1.08 : 1.0) * (impact + exploitability), 10));
    }

    private static double? Weight(Dictionary<string, string> metrics, string name, params (string Value, double Weight)[] weights)
    {
        if (!metrics.TryGetValue(name, out var value))
        {
            return null;
        }

        foreach (var (text, weight) in weights)
        {
            if (text == value)
            {
                return weight;
            }
        }

        return null;
    }

    // The CVSS 3.1 round-up to one decimal, written with integers to avoid floating point error.
    private static double RoundUp(double value)
    {
        var integer = (long)Math.Round(value * 100000);
        return integer % 10000 == 0 ? integer / 100000.0 : (Math.Floor(integer / 10000.0) + 1) / 10.0;
    }
}
