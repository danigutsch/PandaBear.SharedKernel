namespace SharedKernel.Blazor.Site;

/// <summary>Checks a <c>srcset</c>: one descriptor kind, no repeated descriptor, at least one candidate.</summary>
internal static class CandidateSet
{
    /// <summary>Copies and checks the candidates of one <c>srcset</c>.</summary>
    /// <param name="candidates">The candidates.</param>
    /// <param name="parameterName">The parameter reported on failure.</param>
    /// <returns>The candidates, copied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="candidates" /> or one of them is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The set is empty, mixes width and density descriptors, or repeats a descriptor.</exception>
    public static ImageCandidate[] Check(IEnumerable<ImageCandidate> candidates, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(candidates, parameterName);
        var copy = candidates.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("A srcset needs at least one candidate.", parameterName);
        }

        if (copy.Any(candidate => candidate is null))
        {
            throw new ArgumentNullException(parameterName, "A srcset candidate must not be null.");
        }

        var widths = copy[0].Width is not null;
        if (copy.Any(candidate => candidate.Width is not null != widths))
        {
            throw new ArgumentException("A srcset must use width descriptors or density descriptors, not both.", parameterName);
        }

        var descriptors = copy.Select(candidate => candidate.Width is { } width ? width : candidate.Density!.Value);
        if (descriptors.Distinct().Count() != copy.Length)
        {
            throw new ArgumentException("A srcset must not repeat a descriptor.", parameterName);
        }

        return copy;
    }
}
