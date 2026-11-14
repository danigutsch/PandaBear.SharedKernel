using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace PandaBear.HttpCaching.AspNetCore;

/// <summary>
/// An RFC 9110 <c>If-Match</c> condition: <c>*</c> or a list of entity tags, evaluated with strong comparison, so a weak tag never matches.
/// Use it for an optimistic-concurrency write whose resource has an opaque entity tag; answer 412 with <see cref="PreconditionProblems.Failed"/>
/// when <see cref="Matches(EntityTagHeaderValue?)"/> is <see langword="false"/>.
/// </summary>
public sealed class IfMatchCondition
{
    private IfMatchCondition(bool matchesAny, IReadOnlyList<EntityTagHeaderValue> entityTags)
    {
        MatchesAny = matchesAny;
        EntityTags = entityTags;
    }

    /// <summary>Gets a value indicating whether the condition is <c>*</c>: it matches any current representation.</summary>
    public bool MatchesAny { get; }

    /// <summary>Gets the listed entity tags; empty when <see cref="MatchesAny"/> is <see langword="true"/>.</summary>
    public IReadOnlyList<EntityTagHeaderValue> EntityTags { get; }

    /// <summary>
    /// Parses the field values of an <c>If-Match</c> header. The whole value is refused when any element is malformed, when it is empty,
    /// or when <c>*</c> is combined with a tag.
    /// </summary>
    /// <param name="values">The header values, as several header lines or one comma-separated line.</param>
    /// <param name="condition">The condition, or <see langword="null"/> when the value is not a valid <c>If-Match</c>.</param>
    /// <returns><see langword="true"/> when the value is valid.</returns>
    public static bool TryParse(StringValues values, [NotNullWhen(true)] out IfMatchCondition? condition)
    {
        condition = null;

        // The strict parser rejects the whole header when any list element is malformed; the lenient one would silently drop it.
        if (!EntityTagHeaderValue.TryParseStrictList(values, out var parsed) || parsed.Count == 0)
        {
            return false;
        }

        if (parsed.Any(tag => tag.Equals(EntityTagHeaderValue.Any)))
        {
            // RFC 9110 makes "*" an alternative to the list, never a member of it.
            if (parsed.Count != 1)
            {
                return false;
            }

            condition = new IfMatchCondition(matchesAny: true, []);
            return true;
        }

        condition = new IfMatchCondition(matchesAny: false, [.. parsed]);
        return true;
    }

    /// <summary>
    /// Reads the <c>If-Match</c> header of a request that requires one. A missing header is answered with
    /// <see cref="PreconditionProblems.Required"/> (428) and an unusable one with <see cref="PreconditionProblems.InvalidIfMatch"/> (400),
    /// both with their default detail. Call <see cref="TryParse"/> and the <see cref="PreconditionProblems"/> methods instead for other wording.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="condition">The condition, or <see langword="null"/> when a problem is returned.</param>
    /// <returns>The problem to answer, or <see langword="null"/> when <paramref name="condition"/> is set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public static ProblemHttpResult? Read(HttpRequest request, out IfMatchCondition? condition)
    {
        ArgumentNullException.ThrowIfNull(request);

        condition = null;
        if (!request.Headers.TryGetValue(HeaderNames.IfMatch, out var values))
        {
            return PreconditionProblems.Required();
        }

        return TryParse(values, out condition) ? null : PreconditionProblems.InvalidIfMatch();
    }

    /// <summary>Evaluates the condition against the current representation.</summary>
    /// <param name="current">The current entity tag, or <see langword="null"/> when the resource does not exist.</param>
    /// <returns>
    /// <see langword="true"/> when the resource exists and the condition is <c>*</c> or lists a tag strongly equal to <paramref name="current"/>.
    /// </returns>
    public bool Matches(EntityTagHeaderValue? current)
    {
        if (current is null)
        {
            return false;
        }

        return MatchesAny || EntityTags.Any(tag => tag.Compare(current, useStrongComparison: true));
    }

    /// <summary>Evaluates the condition against the current entity tag given as its header text, such as <c>"7-12"</c>.</summary>
    /// <param name="currentEntityTag">The current entity tag, or <see langword="null"/> when the resource does not exist.</param>
    /// <returns><see langword="true"/> when the condition matches; an unparseable tag never matches.</returns>
    public bool Matches(string? currentEntityTag) =>
        currentEntityTag is not null && EntityTagHeaderValue.TryParse(currentEntityTag, out var current) && Matches(current);
}
