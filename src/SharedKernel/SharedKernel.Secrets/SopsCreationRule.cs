using System.Text.RegularExpressions;
using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Secrets;

/// <summary>
/// A SOPS creation rule: files whose path matches a regular expression are encrypted to exactly one recipient set, and that
/// set has at least a given number of recipients. The minimum is a parameter of the rule, not a constant of the library.
/// The expression is unanchored like SOPS's <c>path_regex</c>, compiled with <see cref="RegexOptions.NonBacktracking"/> and
/// a one-second match timeout, so it has no backtracking and no look-around or back-reference.
/// </summary>
public sealed class SopsCreationRule : ValueObject
{
    /// <summary>The longest accepted expression.</summary>
    public const int MaxPatternLength = 1024;

    /// <summary>The most recipients a rule can require.</summary>
    public const int MaxMinimumRecipients = 64;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private readonly Regex _regex;

    private SopsCreationRule(string pathRegex, Regex regex, AgeRecipientSet recipients, int minimumRecipients)
    {
        PathRegex = pathRegex;
        _regex = regex;
        Recipients = recipients;
        MinimumRecipients = minimumRecipients;
    }

    /// <summary>Gets the path expression.</summary>
    public string PathRegex
    {
        get;
    }

    /// <summary>Gets the recipients that every matching file is encrypted to, and only those.</summary>
    public AgeRecipientSet Recipients
    {
        get;
    }

    /// <summary>Gets the least number of recipients that the rule allows.</summary>
    public int MinimumRecipients
    {
        get;
    }

    /// <summary>Creates a rule.</summary>
    /// <param name="pathRegex">The path expression; it is not trimmed.</param>
    /// <param name="recipients">The exact recipient set.</param>
    /// <param name="minimumRecipients">The least number of recipients, from 1 to 64; <paramref name="recipients"/> must have at least that many.</param>
    /// <returns>The rule or a validation error that never echoes the expression.</returns>
    public static Result<SopsCreationRule> Create(string? pathRegex, AgeRecipientSet? recipients, int minimumRecipients)
    {
        if (recipients is null)
        {
            return Invalid("recipients", "Recipients are required.");
        }

        if (minimumRecipients is < 1 or > MaxMinimumRecipients)
        {
            return Invalid("minimumRecipients", $"Minimum recipients must be from 1 to {MaxMinimumRecipients}.");
        }

        if (recipients.Count < minimumRecipients)
        {
            return Invalid("recipients", "The rule has fewer recipients than its minimum.");
        }

        if (string.IsNullOrEmpty(pathRegex))
        {
            return Invalid("pathRegex", "Path expression is required.");
        }

        if (pathRegex.Length > MaxPatternLength)
        {
            return Invalid("pathRegex", $"Path expression cannot exceed {MaxPatternLength} characters.");
        }

        Regex regex;
        try
        {
            regex = new Regex(pathRegex, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, MatchTimeout);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return Invalid("pathRegex", "Path expression is not a valid regular expression that the non-backtracking engine supports.");
        }

        return Result.Ok(new SopsCreationRule(pathRegex, regex, recipients, minimumRecipients));
    }

    /// <summary>Returns whether the rule applies to <paramref name="path"/>.</summary>
    /// <param name="path">The relative file path.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="RegexMatchTimeoutException">Matching took longer than one second.</exception>
    public bool Matches(EncryptedFilePath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return _regex.IsMatch(path.Value);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PathRegex;
        yield return Recipients;
        yield return MinimumRecipients;
    }

    private static Result<SopsCreationRule> Invalid(string field, string message) => Result.Invalid<SopsCreationRule>(message, field, message);
}
