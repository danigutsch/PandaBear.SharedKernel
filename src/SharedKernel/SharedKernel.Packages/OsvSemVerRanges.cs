namespace SharedKernel.Packages;

/// <summary>Reads the OSV SemVer intervals that have recognizable introductions and explicit fix boundaries.</summary>
internal static class OsvSemVerRanges
{
    /// <summary>Reads supported intervals. Exact version queries still supply membership for unsupported wire ranges.</summary>
    internal static AdvisoryVersionRange[] Read(IEnumerable<OsvWire.Range>? ranges) =>
        [.. (ranges ?? []).SelectMany(static range => ReadRange(range))];

    private static AdvisoryVersionRange[] ReadRange(OsvWire.Range? range)
    {
        if (range is not { Type: "SEMVER", Events: { Count: > 0 } events }
            || events.Any(static item => item is null || item.LastAffected is not null || item.Limit is not null))
        {
            return [];
        }

        List<AdvisoryVersionRange> intervals = [];
        for (var index = 0; index < events.Count; index += 2)
        {
            var start = events[index];
            var end = index + 1 < events.Count ? events[index + 1] : null;
            if (start is not { Introduced: { } introduced, Fixed: null }
                || (end is not null && end is not { Introduced: null, Fixed: not null })
                || !TryVersion(introduced == "0" ? null : introduced, out var lower)
                || !TryVersion(end?.Fixed, out var upper))
            {
                return [];
            }

            var interval = AdvisoryVersionRange.Create(lower, upper);
            if (interval.IsFailure || (intervals.LastOrDefault()?.FixedVersion is { } previous
                && (lower is null || PackageVersionPrecedence.CompareSemVer(lower, previous) is not >= 0)))
            {
                return [];
            }

            intervals.Add(interval.Value);
        }

        return [.. intervals];
    }

    private static bool TryVersion(string? text, out PackageVersion? version)
    {
        version = null;
        if (text is null)
        {
            return true;
        }

        var parsed = PackageVersion.Create(text);
        version = parsed.IsSuccess ? parsed.Value : null;
        return parsed.IsSuccess;
    }
}
