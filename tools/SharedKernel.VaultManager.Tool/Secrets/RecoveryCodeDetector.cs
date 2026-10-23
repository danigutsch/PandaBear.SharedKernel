using System.Globalization;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>
/// Finds blocks of two-factor recovery codes: 5 to 25 codes on up to 20 lines, every code with the same length and
/// separator positions, with optional numbering and used markers. Digit-only blocks need a keyword, a known file name,
/// or a matching provider; dates, sequences, and shared-prefix runs are rejected.
/// </summary>
internal static class RecoveryCodeDetector
{
    /// <summary>Rule identifier of block findings.</summary>
    public const string RuleId = "recovery-code-block";

    private const int MinimumCodes = 5;
    private const int StrongCodes = 6;
    private const int MaximumCodes = 25;
    private const int MaximumLines = 20;

    private static readonly string[] Keywords =
    [
        "backup code", "recovery code", "scratch code", "one-time code", "one time code", "emergency code", "2fa", "mfa",
        "two-factor", "two factor", "keep these somewhere safe", "each code can be used once", "recovery key", "secret key",
        "emergency kit", "wiederherstellungscode", "códigos de respaldo", "codes de secours", "backup-code", "recovery-code",
    ];

    // Provider shapes: host suffix, code count range, code length without separators, and charset.
    private static readonly (string Host, int MinCount, int MaxCount, int Length, CodeCharset Charset)[] Profiles =
    [
        ("google.com", 10, 10, 8, CodeCharset.Digits),
        ("github.com", 16, 16, 10, CodeCharset.LowerHex),
        ("gitlab.com", 10, 10, 16, CodeCharset.LowerHex),
        ("pypi.org", 8, 8, 16, CodeCharset.LowerHex),
        ("dropbox.com", 10, 10, 8, CodeCharset.LowerAlphanumeric),
    ];

    /// <summary>Whether text mentions recovery or backup codes.</summary>
    /// <param name="text">Text.</param>
    /// <returns><see langword="true"/> when a keyword is present.</returns>
    public static bool HasKeyword(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Finds recovery code blocks.</summary>
    /// <param name="text">Field text.</param>
    /// <param name="fieldPath">Field path for locations.</param>
    /// <param name="fileNameHint">Whether the text comes from a file whose name marks it as recovery codes.</param>
    /// <param name="host">Login host, used to pick a provider profile.</param>
    /// <returns>Findings with the recovery codes target.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static List<SecretFinding> Detect(string text, string fieldPath, bool fileNameHint, string? host)
    {
        ArgumentNullException.ThrowIfNull(text);
        var findings = new List<SecretFinding>();
        var lines = SecretText.Lines(text);
        bool keyword = fileNameHint || HasKeyword(text);
        int index = 0;
        while (index < lines.Count)
        {
            List<string>? codes = ParseCodes(lines[index].Text);
            if (codes is null)
            {
                index++;
                continue;
            }

            string shape = Shape(codes[0]);
            int first = index;
            int last = index;
            var block = new List<string>(codes);
            int next = index + 1;
            while (next < lines.Count && next - first < MaximumLines + 2)
            {
                if (lines[next].Text.Trim().Length == 0 && next + 1 < lines.Count && ParseCodes(lines[next + 1].Text) is { } afterBlank && Shape(afterBlank[0]) == shape)
                {
                    next++;
                    continue;
                }

                List<string>? more = ParseCodes(lines[next].Text);
                if (more is null || more.Any(code => Shape(code) != shape))
                {
                    break;
                }

                block.AddRange(more);
                last = next;
                next++;
            }

            if (codes.All(code => Shape(code) == shape) && Accept(block, last - first + 1, keyword, host, out Confidence confidence))
            {
                int start = lines[first].Start + LeadingWhitespace(lines[first].Text);
                int end = lines[last].Start + lines[last].Text.TrimEnd().Length;
                findings.Add(new SecretFinding
                {
                    RuleId = RuleId,
                    Kind = SecretKind.RecoveryCodes,
                    Confidence = confidence,
                    Location = new SecretLocation(fieldPath, start, end - start, first + 1, last + 1),
                    ProposedTarget = PlacementTarget.Hidden("Recovery codes"),
                    Message = string.Create(CultureInfo.InvariantCulture, $"Block of {block.Count} recovery codes"),
                });
            }

            index = last + 1;
        }

        return findings;
    }

    private static bool Accept(List<string> codes, int lineCount, bool keyword, string? host, out Confidence confidence)
    {
        confidence = Confidence.Low;
        if (codes.Count is < MinimumCodes or > MaximumCodes || lineCount > MaximumLines)
        {
            return false;
        }

        string[] compact = [.. codes.Select(code => code.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal))];
        CodeCharset charset = Charset(compact);
        bool profile = MatchesProfile(host, compact, charset);
        if (codes.Any(IsDateOrTime) || compact.Distinct(StringComparer.Ordinal).Count() < compact.Length
            || SharesLongPrefix(compact) || IsNumericSequence(compact))
        {
            return false;
        }

        bool context = keyword || profile;
        if (charset == CodeCharset.Digits && !context)
        {
            return false;
        }

        if (!context && compact.Count(code => code.Any(char.IsAsciiDigit)) * 2 < compact.Length)
        {
            return false;
        }

        confidence = (context, codes.Count >= StrongCodes) switch
        {
            (true, true) => Confidence.High,
            (true, false) or (false, true) => Confidence.Medium,
            _ => Confidence.Low,
        };
        return true;
    }

    // A code whose date check times out, even when run again, counts as no date, so its block stays a finding to review.
    private static bool IsDateOrTime(string code) =>
        PatternRetry.TryRun(() => SecretPatterns.DateOrTime().IsMatch(code), out bool date) && date;

    private static List<string>? ParseCodes(string line)
    {
        string rest = line.Trim();
        if (rest.Length == 0)
        {
            return null;
        }

        rest = StripNumbering(rest);
        rest = StripUsedMarker(rest);
        if (rest.StartsWith("~~", StringComparison.Ordinal) && rest.EndsWith("~~", StringComparison.Ordinal) && rest.Length > 4)
        {
            rest = rest[2..^2].Trim();
        }

        var codes = new List<string>();
        foreach (string token in rest.Split(["  ", "\t"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!IsCode(token))
            {
                return null;
            }

            codes.Add(token);
        }

        return codes.Count is > 0 and <= 4 ? codes : null;
    }

    private static bool IsCode(string token)
    {
        if (token.Length is < 5 or > 40 || !char.IsAsciiLetterOrDigit(token[0]) || !char.IsAsciiLetterOrDigit(token[^1]))
        {
            return false;
        }

        int segments = 1;
        int segmentLength = 0;
        foreach (char character in token)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                segmentLength++;
                continue;
            }

            if (character is not (' ' or '-') || segmentLength < 3)
            {
                return false;
            }

            segments++;
            segmentLength = 0;
        }

        return segments <= 8 && segmentLength >= 3;
    }

    private static string StripNumbering(string line)
    {
        int index = 0;
        while (index < line.Length && index < 3 && char.IsAsciiDigit(line[index]))
        {
            index++;
        }

        if (index > 0 && index < line.Length - 1 && line[index] is '.' or ')' or ':' && line[index + 1] is ' ' or '\t')
        {
            return line[(index + 1)..].TrimStart();
        }

        if (line.Length > 2 && line[0] is '-' or '*' or '•' && line[1] == ' ')
        {
            return line[2..].TrimStart();
        }

        if (line.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase) || line.StartsWith("[ ] ", StringComparison.Ordinal))
        {
            return line[4..].TrimStart();
        }

        return line;
    }

    private static string StripUsedMarker(string line)
    {
        foreach (string marker in new[] { "(used)", "[used]", "used", "✓", "✔", "(x)", "x" })
        {
            if (line.Length > marker.Length + 1 && line.EndsWith(" " + marker, StringComparison.OrdinalIgnoreCase))
            {
                return line[..^(marker.Length + 1)].TrimEnd();
            }
        }

        return line;
    }

    private static string Shape(string code)
    {
        return string.Create(code.Length, code, static (span, source) =>
        {
            for (int index = 0; index < source.Length; index++)
            {
                span[index] = source[index] is ' ' or '-' ? source[index] : 'c';
            }
        });
    }

    private static CodeCharset Charset(string[] codes)
    {
        bool digitsOnly = codes.All(code => code.All(char.IsAsciiDigit));
        if (digitsOnly)
        {
            return CodeCharset.Digits;
        }

        if (codes.All(code => code.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f')))
        {
            return CodeCharset.LowerHex;
        }

        if (codes.All(code => code.All(character => char.IsAsciiDigit(character) || char.IsAsciiLetterLower(character))))
        {
            return CodeCharset.LowerAlphanumeric;
        }

        return codes.All(code => code.All(character => char.IsAsciiDigit(character) || char.IsAsciiLetterUpper(character)))
            ? CodeCharset.UpperAlphanumeric
            : CodeCharset.Mixed;
    }

    private static bool MatchesProfile(string? host, string[] codes, CodeCharset charset)
    {
        if (host is null)
        {
            return false;
        }

        foreach (var profile in Profiles)
        {
            if ((host == profile.Host || host.EndsWith("." + profile.Host, StringComparison.Ordinal))
                && codes.Length >= profile.MinCount && codes.Length <= profile.MaxCount
                && codes[0].Length == profile.Length
                && (charset == profile.Charset || (profile.Charset == CodeCharset.LowerAlphanumeric && charset is CodeCharset.LowerHex or CodeCharset.Digits)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SharesLongPrefix(string[] codes)
    {
        int prefix = codes[0].Length;
        foreach (string code in codes)
        {
            int common = 0;
            while (common < prefix && common < code.Length && code[common] == codes[0][common])
            {
                common++;
            }

            prefix = common;
        }

        return prefix * 2 >= codes[0].Length;
    }

    private static bool IsNumericSequence(string[] codes)
    {
        if (!codes.All(code => code.Length <= 18 && code.All(char.IsAsciiDigit)))
        {
            return false;
        }

        long[] values = [.. codes.Select(code => long.Parse(code, NumberStyles.None, CultureInfo.InvariantCulture))];
        return values.Zip(values.Skip(1)).All(pair => Math.Abs(pair.Second - pair.First) == 1);
    }

    private static int LeadingWhitespace(string line)
    {
        return line.Length - line.TrimStart().Length;
    }

    private enum CodeCharset
    {
        Digits,
        LowerHex,
        LowerAlphanumeric,
        UpperAlphanumeric,
        Mixed,
    }
}
