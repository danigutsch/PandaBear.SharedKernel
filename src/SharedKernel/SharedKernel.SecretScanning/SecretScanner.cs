using System.Buffers;
using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;
using SharedKernel.BuildingBlocks;

namespace SharedKernel.SecretScanning;

/// <summary>
/// Finds secrets in text. Catalog rules run only on bounded windows around keyword hits; structural detectors cover
/// labeled lines, security questions, recovery code blocks, card numbers, IBANs, and seed phrases. Findings hold
/// locations, never values; <see cref="Scan{T}"/> hands each value to the caller's projection once, as it is found.
/// </summary>
/// <remarks>
/// <para>
/// Scanning time grows about linearly with the text: whole-text facts are computed once per field, line lookups use
/// precomputed line starts, and overlapping findings are resolved through an <see cref="OverlapIndex"/>.
/// </para>
/// <para>
/// A pattern match that exceeds its timeout runs once more (<see cref="PatternRetry"/>). When it times out again, the
/// window or line it ran on becomes a <see cref="SecretKind.NeedsReview"/> finding, which takes the place of every
/// finding that overlaps it. Once three windows of one catalog rule time out in a text, the rule's remaining windows are
/// not matched and become one such finding.
/// </para>
/// </remarks>
public sealed class SecretScanner
{
    /// <summary>Rule identifier of Steam Desktop Authenticator shared secrets, which are base64 rather than base32.</summary>
    public const string MaFileSharedSecretRule = "steam-mafile-shared-secret";

    private const int WindowBefore = 1024;
    private const int WindowAfter = 3072;
    private const int MaxWindow = 4096;
    private const int AwsContextLines = 5;

    // Overlapping windows of one rule are merged up to this length, so text with a keyword on every line is scanned about
    // once instead of once per keyword. Every keyword hit still gets its whole window inside one scanned window.
    private const int MaxMergedWindow = 16 * 1024;

    // Once this many windows of one rule time out in a text, even when run again, the rule's remaining windows are not
    // matched and become one finding to review, so a text that the pattern cannot handle costs a bounded time.
    private const int MaxTimedOutWindows = 3;

    private static readonly string[] QuestionContextWords = ["security question", "secret question", "sicherheitsfrage", "pregunta de seguridad"];

    private readonly IReadOnlyList<SecretRule> rules;
    private readonly SearchValues<string> keywords;

    /// <summary>Creates a scanner over a rule set.</summary>
    /// <param name="rules">Catalog rules, in priority order.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is <see langword="null"/>.</exception>
    internal SecretScanner(IReadOnlyList<SecretRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        this.rules = rules;
        RuleIds = rules.Select(rule => rule.Id).Concat(DetectorRuleIds.All).ToFrozenSet(StringComparer.Ordinal);
        keywords = SearchValues.Create([.. rules.SelectMany(rule => rule.Keywords).Distinct(StringComparer.OrdinalIgnoreCase)], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Scanner over the built-in catalog.</summary>
    public static SecretScanner Default { get; } = new(SecretRuleCatalog.All);

    /// <summary>
    /// Identifiers of every rule whose findings this scanner can report: its catalog rules and its structural detectors. A
    /// caller that maps rule identifiers to its own decisions can check its map against them.
    /// </summary>
    public IReadOnlySet<string> RuleIds { get; }

    /// <summary>Scans <paramref name="text"/> and returns what it finds, without the values.</summary>
    /// <param name="text">The text.</param>
    /// <param name="location">The name the findings give the text, such as a field path or a file name.</param>
    /// <param name="context">What is known about where the text comes from.</param>
    /// <returns>The findings in the order of the text.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public IReadOnlyList<DetectedSecret> Scan(string text, string location, SecretScanContext context) =>
        Scan(text, location, context, static (secret, _) => secret);

    /// <summary>
    /// Scans <paramref name="text"/> and turns each finding into a <typeparamref name="T"/> with
    /// <paramref name="project"/>, which also receives the found value: the text of one secret, or <see langword="null"/>
    /// when the finding spans text that is not one value, such as a seed phrase, a block of recovery codes, or a region the
    /// patterns timed out on. The scanner keeps no value after the projection returns.
    /// </summary>
    /// <typeparam name="T">The caller's finding type.</typeparam>
    /// <param name="text">The text.</param>
    /// <param name="location">The name the findings give the text, such as a field path or a file name.</param>
    /// <param name="context">What is known about where the text comes from.</param>
    /// <param name="project">Turns a finding and its value into the caller's type.</param>
    /// <returns>The projected findings in the order of the text.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public IReadOnlyList<T> Scan<T>(string text, string location, SecretScanContext context, Func<DetectedSecret, string?, T> project)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(project);
        List<Hit> found = context.CatalogRulesOnly ? ScanCatalog(text, location) : ScanAll(text, location, context);
        List<Hit> hits = [.. found.OrderBy(hit => hit.Location.Start)];
        var result = new List<T>(hits.Count);
        foreach (Hit hit in hits)
        {
            result.Add(project(hit.ToDetected(), hit.Value));
        }

        return result;
    }

    private List<Hit> ScanAll(string text, string path, SecretScanContext context)
    {
        string? fileName = context.FileName;
        var findings = new List<Hit>();
        if (fileName is not null && fileName.EndsWith(".maFile", StringComparison.OrdinalIgnoreCase))
        {
            findings.AddRange(ScanMaFile(text, path));
        }

        if (fileName is not null && fileName.Equals("MEGA-RECOVERYKEY.txt", StringComparison.OrdinalIgnoreCase))
        {
            string trimmed = text.Trim();
            if (trimmed.Length is >= 16 and <= 128 && !SecretText.HasWhitespace(trimmed))
            {
                findings.Add(Finding(DetectorRuleIds.MegaRecoveryKey, SecretKind.RecoveryCodes, Confidence.High, new SecretLocation(path, text.IndexOf(trimmed, StringComparison.Ordinal), trimmed.Length), "MEGA recovery key", trimmed));
            }
        }

        findings.AddRange(ScanField(text, path, context, IsRecoveryFileName(fileName)));
        return findings;
    }

    private static bool IsRecoveryFileName(string? fileName)
    {
        if (fileName is null)
        {
            return false;
        }

        string name = fileName.ToUpperInvariant();
        bool text = name.EndsWith(".TXT", StringComparison.Ordinal);
        int recovery = name.IndexOf("RECOVERY", StringComparison.Ordinal);
        return name.EndsWith(".MAFILE", StringComparison.Ordinal)
            || name is "GITHUB-RECOVERY-CODES.TXT" or "GITLAB-RECOVERY-CODES.TXT" or "MEGA-RECOVERYKEY.TXT"
            || (text && name.StartsWith("BACKUP-CODES-", StringComparison.Ordinal))
            || (text && name.Contains("-BACKUP-CODES", StringComparison.Ordinal))
            || (text && recovery >= 0 && name.IndexOf("CODE", recovery, StringComparison.Ordinal) > 0);
    }

    private List<Hit> ScanField(string text, string path, SecretScanContext context, bool fileNameHint)
    {
        List<Hit> seeds = ScanSeedPhrases(text, path);
        var raw = new List<Hit>();
        raw.AddRange(ScanCatalog(text, path));
        raw.AddRange(ScanAwsContext(text, path, raw));
        bool seedNearby = seeds.Count > 0 || raw.Any(finding => finding.Kind == SecretKind.SeedPhrase);
        raw.AddRange(ScanLines(text, path, context, seedNearby));
        raw.AddRange(RecoveryCodeDetector.Detect(text, path, fileNameHint, context.Host));
        raw.AddRange(ScanCardNumbers(text, path));
        raw.AddRange(ScanIbans(text, path));
        raw.AddRange(seeds);
        raw.AddRange(ScanHighEntropy(text, path));
        return Deduplicate(raw);
    }

    private List<Hit> ScanCatalog(string text, string path)
    {
        var findings = new List<Hit>();
        var windows = new Dictionary<SecretRule, List<(int Start, int End)>>();
        ReadOnlySpan<char> span = text;
        int position = 0;
        while (position < span.Length)
        {
            int hit = span[position..].IndexOfAny(keywords);
            if (hit < 0)
            {
                break;
            }

            hit += position;
            foreach (SecretRule rule in rules)
            {
                if (rule.Keywords.Any(keyword => text.AsSpan(hit).StartsWith(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    AddWindow(windows, rule, Math.Max(0, hit - WindowBefore), Math.Min(text.Length, hit + WindowAfter));
                }
            }

            position = hit + 1;
        }

        var seen = new HashSet<(string, int, int)>();
        foreach (SecretRule rule in rules)
        {
            if (!windows.TryGetValue(rule, out var ruleWindows))
            {
                continue;
            }

            int timedOut = 0;
            foreach (var (start, end) in ruleWindows)
            {
                if (timedOut == MaxTimedOutWindows)
                {
                    int rest = ruleWindows[^1].End - start;
                    findings.Add(TimeoutFinding(rule.Id, path, start, rest, string.Create(CultureInfo.InvariantCulture, $"{rule.DisplayName} pattern timed out {MaxTimedOutWindows} times; review the rest of this text manually")));
                    break;
                }

                if (!PatternRetry.TryRun(() => MatchWindow(rule, text, path, start, end), out var matched))
                {
                    // The window becomes a finding to review; the rule's later windows are still matched.
                    timedOut++;
                    findings.Add(TimeoutFinding(rule.Id, path, start, end - start, $"{rule.DisplayName} pattern timed out; review this text manually"));
                    continue;
                }

                foreach (Hit finding in matched)
                {
                    if (seen.Add((rule.Id, finding.Location.Start, finding.Location.Length)))
                    {
                        findings.Add(finding);
                    }
                }
            }
        }

        return findings;
    }

    private static List<Hit> MatchWindow(SecretRule rule, string text, string path, int start, int end)
    {
        var findings = new List<Hit>();
        for (Match match = rule.Pattern.Match(text, start, end - start); match.Success; match = match.NextMatch())
        {
            if (ToFinding(rule, text, path, match, start, end) is { } finding)
            {
                findings.Add(finding);
            }
        }

        return findings;
    }

    private static void AddWindow(Dictionary<SecretRule, List<(int Start, int End)>> windows, SecretRule rule, int start, int end)
    {
        if (!windows.TryGetValue(rule, out var list))
        {
            list = [];
            windows.Add(rule, list);
        }

        if (list.Count > 0 && start <= list[^1].End && end - list[^1].Start <= MaxMergedWindow)
        {
            list[^1] = (list[^1].Start, Math.Max(end, list[^1].End));
        }
        else if (list.Count == 0 || end > list[^1].End)
        {
            list.Add((start, end));
        }
    }

    private static Hit? ToFinding(SecretRule rule, string text, string path, Match match, int windowStart, int windowEnd)
    {
        int start = match.Index;
        int end = match.Index + match.Length;
        if (rule.Extent is { } extent)
        {
            if (extent(text, start) is not { } structureEnd)
            {
                return null;
            }

            end = structureEnd;
        }
        else
        {
            if ((end == windowEnd && windowEnd < text.Length) || (start == windowStart && windowStart > 0))
            {
                return null;
            }

            if (match.Groups["secret"] is { Success: true } secret)
            {
                start = secret.Index;
                end = secret.Index + secret.Length;
            }
        }

        string value = text[start..end];
        if (CandidateFilter.IsPlaceholder(value.Trim()) || (rule.MinimumEntropy > 0 && SecretText.ShannonEntropy(value) < rule.MinimumEntropy))
        {
            return null;
        }

        Confidence confidence = rule.Confidence;
        string message = rule.DisplayName;
        if (rule.Validator is { } validator && !validator(value))
        {
            if (rule.ConfidenceWhenInvalid is not { } fallback)
            {
                return null;
            }

            confidence = fallback;
            message += " (checksum or structure check failed)";
        }

        bool review = rule.Kind is SecretKind.PuttyKey || rule.Id == "otpauth-hotp";
        return Finding(rule.Id, rule.Kind, confidence, new SecretLocation(path, start, end - start), message, value) with
        {
            RuleName = rule.DisplayName,
            NeedsReview = review,
        };
    }

    private List<Hit> ScanAwsContext(string text, string path, List<Hit> existing)
    {
        var findings = new List<Hit>();
        SecretRule? rule = rules.FirstOrDefault(candidate => candidate.Id == "aws-secret-access-key");
        var keyIds = existing.Where(finding => finding.RuleId == "aws-access-key-id").ToList();
        if (rule is null || keyIds.Count == 0)
        {
            return findings;
        }

        var lines = SecretText.Lines(text);
        var scanned = new HashSet<int>();
        foreach (Hit keyId in keyIds)
        {
            int keyLine = LineIndex(lines, keyId.Location.Start);
            for (int line = Math.Max(0, keyLine - AwsContextLines); line <= Math.Min(lines.Count - 1, keyLine + AwsContextLines); line++)
            {
                var (lineStart, lineText) = lines[line];
                if (lineText.Length > MaxWindow || !scanned.Add(line))
                {
                    continue;
                }

                if (PatternRetry.TryRun(() => AwsSecretCandidates(rule, lineText, lineStart, path), out var candidates))
                {
                    findings.AddRange(candidates);
                }
                else
                {
                    findings.Add(TimeoutFinding(rule.Id, path, lineStart, lineText.Length));
                }
            }
        }

        return findings;
    }

    private static List<Hit> AwsSecretCandidates(SecretRule rule, string lineText, int lineStart, string path)
    {
        var findings = new List<Hit>();
        for (Match match = SecretPatterns.AwsSecretCandidate().Match(lineText); match.Success; match = match.NextMatch())
        {
            if (SecretText.ShannonEntropy(match.Value) >= rule.MinimumEntropy && SecretText.CharacterClasses(match.Value) >= 3)
            {
                var location = new SecretLocation(path, lineStart + match.Index, match.Length);
                findings.Add(Finding(rule.Id, rule.Kind, Confidence.Medium, location, rule.DisplayName + " near an access key ID", match.Value) with { RuleName = rule.DisplayName });
            }
        }

        return findings;
    }

    // Lines that hold only whitespace never hold a finding, so the scan steps over them; "next" is the following line that
    // holds text.
    private static List<Hit> ScanLines(string text, string path, SecretScanContext context, bool seedNearby)
    {
        var findings = new List<Hit>();
        var allLines = SecretText.Lines(text);
        List<(int Start, string Text)> lines = [.. allLines.Where(line => !string.IsNullOrWhiteSpace(line.Text))];
        var parsed = new (string Key, int KeyStart, KeyClass Class, int ValueStart, string Value)?[lines.Count];
        for (int index = 0; index < lines.Count; index++)
        {
            string line = lines[index].Text;
            if (line.Length > MaxWindow)
            {
                continue;
            }

            if (!PatternRetry.TryRun(() => SecretPatterns.NotesLine().Match(line), out var match))
            {
                findings.Add(TimeoutFinding(DetectorRuleIds.NotesLine, path, lines[index].Start, line.Length));
            }
            else if (match.Success)
            {
                Group value = match.Groups["value"];
                Group key = match.Groups["key"];
                parsed[index] = (key.Value, lines[index].Start + key.Index, KeyClassifier.Classify(key.Value), lines[index].Start + value.Index, value.Value);
            }
        }

        bool? questionContext = null;
        var consumed = new HashSet<int>();
        for (int index = 0; index < lines.Count; index++)
        {
            if (consumed.Contains(index))
            {
                continue;
            }

            int next = index + 1 < lines.Count ? index + 1 : -1;
            if (parsed[index] is { Class: KeyClass.Question } question)
            {
                if (next >= 0 && parsed[next] is { Class: KeyClass.Answer } answer && !CandidateFilter.IsPlaceholder(answer.Value))
                {
                    findings.Add(QuestionFinding(path, question.ValueStart, question.Value.Length, answer.ValueStart, answer.Value, LineNumber(allLines, lines[index].Start), LineNumber(allLines, lines[next].Start), Confidence.Medium));
                    consumed.Add(next);
                }

                continue;
            }

            if (parsed[index] is { } entry && entry.Class != KeyClass.None)
            {
                if (LineFinding(entry.Key, entry.KeyStart, entry.Class, entry.ValueStart, entry.Value, LineNumber(allLines, lines[index].Start), path, context, seedNearby) is { } finding)
                {
                    findings.Add(finding);
                }

                continue;
            }

            string trimmed = lines[index].Text.Trim();
            if (trimmed.EndsWith('?', StringComparison.Ordinal) && trimmed.Length is > 3 and <= 120 && next >= 0 && parsed[next] is null or { Class: KeyClass.None })
            {
                string answerLine = lines[next].Text.Trim();
                if (answerLine.Length is > 0 and <= 100 && !answerLine.EndsWith('?', StringComparison.Ordinal))
                {
                    int questionStart = lines[index].Start + lines[index].Text.IndexOf(trimmed, StringComparison.Ordinal);
                    int answerStart = lines[next].Start + lines[next].Text.IndexOf(answerLine, StringComparison.Ordinal);
                    questionContext ??= QuestionContextWords.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
                    Confidence confidence = questionContext.Value ? Confidence.Medium : Confidence.Suggestion;
                    findings.Add(QuestionFinding(path, questionStart, trimmed.Length, answerStart, answerLine, LineNumber(allLines, lines[index].Start), LineNumber(allLines, lines[next].Start), confidence));
                    consumed.Add(next);
                }
            }
        }

        return findings;
    }

    private static Hit QuestionFinding(string path, int questionStart, int questionLength, int answerStart, string answer, int questionLine, int answerLine, Confidence confidence)
    {
        var location = new SecretLocation(path, answerStart, answer.Length, questionLine, answerLine);
        Hit finding = Finding(DetectorRuleIds.SecurityQuestion, SecretKind.SecurityQuestion, confidence, location, "Security question and answer", answer);
        return finding with
        {
            LabelLocation = new SecretLocation(path, questionStart, questionLength)
        };
    }

    private static Hit? LineFinding(string key, int keyStart, KeyClass keyClass, int start, string value, int line, string path, SecretScanContext context, bool seedNearby)
    {
        var location = new SecretLocation(path, start, value.Length, line, line);
        bool rejected = CandidateFilter.IsRejected(value);
        string normalizedKey = KeyClassifier.Normalize(key);
        switch (keyClass)
        {
            case KeyClass.Password when !rejected:
                bool bip39Passphrase = seedNearby && normalizedKey is "passphrase" or "bip39passphrase";
                return Finding(bip39Passphrase ? DetectorRuleIds.NotesPassphrase : DetectorRuleIds.NotesPassword, SecretKind.Password, Confidence.Medium, location, "Password line", value);
            case KeyClass.Pin when value.Length is >= 3 and <= 12 && !SecretText.HasWhitespace(value) && !CandidateFilter.IsPlaceholder(value):
                return Finding(DetectorRuleIds.NotesPin, SecretKind.Pin, Confidence.Medium, location, "PIN line", value);
            case KeyClass.Username when context.ExpectsUsername && !CandidateFilter.IsPlaceholder(value) && value.Length <= 256:
                return Finding(DetectorRuleIds.NotesUsername, SecretKind.Username, Confidence.Medium, location, "User name line", value);
            case KeyClass.Token when !rejected && value.Length is >= 8 and <= 512 && !SecretText.HasWhitespace(value) && SecretText.ShannonEntropy(value) >= 3.0:
                var label = new SecretLocation(path, keyStart, key.Length);
                return Finding(DetectorRuleIds.NotesToken, SecretKind.ApiToken, Confidence.Low, location, "Token line", value) with
                {
                    LabelLocation = label
                };
            case KeyClass.Recovery when context.IsBitwardenAccount && IsBitwardenRecoveryCode(value):
                return Finding(DetectorRuleIds.BitwardenRecoveryCode, SecretKind.BitwardenRecoveryCode, Confidence.Medium, location, "Bitwarden account recovery code", value) with
                {
                    NeedsReview = true
                };
            case KeyClass.Recovery when !rejected && value.Length >= 8:
                return Finding(DetectorRuleIds.NotesRecoveryCode, SecretKind.RecoveryCodes, Confidence.Medium, location, "Recovery code line", value);
            case KeyClass.Seed when value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 12:
                return Finding(DetectorRuleIds.NotesSeed, SecretKind.SeedPhrase, Confidence.Low, location, "Seed phrase line", value);
            case KeyClass.Totp when TotpUri.TryNormalize(value, null, null, out _):
                return Finding(DetectorRuleIds.NotesTotp, SecretKind.Totp, Confidence.Medium, location, "TOTP secret line", value);
            case KeyClass.Bic when Bic.Create(value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant()).IsSuccess:
                return Finding(DetectorRuleIds.Bic, SecretKind.Iban, Confidence.Medium, location, "BIC line", value);
            default:
                return null;
        }
    }

    private static bool IsBitwardenRecoveryCode(string value)
    {
        string compact = value.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        return compact.Length == 32 && compact.All(character => char.IsAsciiDigit(character) || char.IsAsciiLetterLower(character));
    }

    private static List<Hit> ScanCardNumbers(string text, string path)
    {
        var findings = new List<Hit>();
        int index = 0;
        while (index < text.Length)
        {
            if (!char.IsAsciiDigit(text[index]) || (index > 0 && (char.IsAsciiLetterOrDigit(text[index - 1]) || text[index - 1] is '-' or '.' or ',' or '+')))
            {
                index++;
                continue;
            }

            var digits = new System.Text.StringBuilder();
            int position = index;
            int end = index;
            while (position < text.Length && digits.Length < 20)
            {
                char character = text[position];
                if (char.IsAsciiDigit(character))
                {
                    digits.Append(character);
                    position++;
                    end = position;
                }
                else if (character is ' ' or '-' && position + 1 < text.Length && char.IsAsciiDigit(text[position + 1]) && digits.Length < 19)
                {
                    position++;
                }
                else
                {
                    break;
                }
            }

            string number = digits.ToString();
            bool boundary = end >= text.Length || !(char.IsAsciiLetterOrDigit(text[end]) || (text[end] is '.' or ',' && end + 1 < text.Length && char.IsAsciiDigit(text[end + 1])));
            if (boundary && CardNumberValidator.TryGetBrand(number, out string? brand) && !CandidateFilter.IsRepeatedOrSequential(number))
            {
                findings.Add(Finding(DetectorRuleIds.CardNumber, SecretKind.CardNumber, Confidence.High, new SecretLocation(path, index, end - index), brand + " card number", number));
            }

            index = Math.Max(end, index + 1);
        }

        return findings;
    }

    private static List<Hit> ScanIbans(string text, string path)
    {
        var findings = new List<Hit>();
        for (int index = 0; index + 4 <= text.Length; index++)
        {
            if (!(char.IsAsciiLetterUpper(text[index]) && char.IsAsciiLetterUpper(text[index + 1]) && char.IsAsciiDigit(text[index + 2]) && char.IsAsciiDigit(text[index + 3]))
                || (index > 0 && char.IsAsciiLetterOrDigit(text[index - 1])))
            {
                continue;
            }

            var compact = new System.Text.StringBuilder();
            int position = index;
            int bestEnd = -1;
            while (position < text.Length && compact.Length <= 34)
            {
                char character = text[position];
                if (char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character))
                {
                    compact.Append(character);
                    position++;
                    bool atBoundary = position >= text.Length || !char.IsAsciiLetterOrDigit(text[position]);
                    if (atBoundary && compact.Length >= 15 && Iban.Create(compact.ToString()).IsSuccess)
                    {
                        bestEnd = position;
                    }
                }
                else if (character == ' ' && position + 1 < text.Length && char.IsAsciiLetterOrDigit(text[position + 1]))
                {
                    position++;
                }
                else
                {
                    break;
                }
            }

            if (bestEnd > 0)
            {
                findings.Add(Finding(DetectorRuleIds.Iban, SecretKind.Iban, Confidence.High, new SecretLocation(path, index, bestEnd - index), "IBAN", text[index..bestEnd]));
                index = bestEnd;
            }
        }

        return findings;
    }

    private static List<Hit> ScanSeedPhrases(string text, string path)
    {
        var findings = new List<Hit>();
        var run = new List<(string Word, int Start, int End)>();
        int index = 0;
        while (index <= text.Length)
        {
            int start = index;
            while (start < text.Length && text[start] is ' ' or '\t' or '\r' or '\n')
            {
                start++;
            }

            int end = start;
            while (end < text.Length && text[end] is not (' ' or '\t' or '\r' or '\n'))
            {
                end++;
            }

            if (start >= text.Length)
            {
                CheckRun(run, path, findings);
                break;
            }

            string token = text[start..end];
            if (IsNumbering(token))
            {
                index = end;
                continue;
            }

            string word = token.All(char.IsLetter) ? Bip39.NormalizeWord(token) : string.Empty;
            if (word.Length > 0 && Bip39.IsWord(word))
            {
                run.Add((word, start, end));
            }
            else
            {
                CheckRun(run, path, findings);
                run.Clear();
            }

            index = end;
        }

        return findings;
    }

    private static bool IsNumbering(string token)
    {
        return token.Length is >= 2 and <= 4 && token[..^1].All(char.IsAsciiDigit) && token[^1] is '.' or ')' or ':';
    }

    private static void CheckRun(List<(string Word, int Start, int End)> run, string path, List<Hit> findings)
    {
        int offset = 0;
        bool found = false;
        while (run.Count - offset >= 12)
        {
            int matched = 0;
            foreach (int length in Bip39.Lengths)
            {
                if (offset + length <= run.Count && Bip39.IsValidMnemonic([.. run.Skip(offset).Take(length).Select(entry => entry.Word)]))
                {
                    matched = length;
                    break;
                }
            }

            if (matched == 0)
            {
                offset++;
                continue;
            }

            Add(run.Skip(offset).Take(matched).ToList(), DetectorRuleIds.Bip39Seed, "BIP39 seed phrase");
            found = true;
            offset += matched;
        }

        if (!found && run.Count is 12 or 13 or 24 && Bip39.IsElectrumSeed([.. run.Select(entry => entry.Word)]))
        {
            Add(run, DetectorRuleIds.ElectrumSeed, "Electrum seed phrase");
        }

        void Add(List<(string Word, int Start, int End)> words, string ruleId, string message)
        {
            int start = words[0].Start;
            int end = words[^1].End;
            findings.Add(new Hit
            {
                RuleId = ruleId,
                Kind = SecretKind.SeedPhrase,
                Confidence = Confidence.High,
                Location = new SecretLocation(path, start, end - start),
                Message = string.Create(CultureInfo.InvariantCulture, $"{message} ({words.Count} words)"),
            });
        }
    }

    private static List<Hit> ScanHighEntropy(string text, string path)
    {
        var findings = new List<Hit>();
        int index = 0;
        while (index < text.Length)
        {
            while (index < text.Length && text[index] is ' ' or '\t' or '\r' or '\n' or '"' or '\'' or '`' or ',' or ';' or '(' or ')' or '[' or ']' or '<' or '>')
            {
                index++;
            }

            int end = index;
            while (end < text.Length && text[end] is not (' ' or '\t' or '\r' or '\n' or '"' or '\'' or '`' or ',' or ';' or '(' or ')' or '[' or ']' or '<' or '>'))
            {
                end++;
            }

            int tokenEnd = end;
            while (tokenEnd > index && text[tokenEnd - 1] is '.' or ':' or '!' or '?')
            {
                tokenEnd--;
            }

            int length = tokenEnd - index;
            if (length is >= 24 and <= 512)
            {
                string token = text[index..tokenEnd];
                if (SecretText.IsBase64Alphabet(token) && SecretText.CharacterClasses(token) >= 2 && SecretText.ShannonEntropy(token) >= 4.5 && !CandidateFilter.IsRejected(token))
                {
                    findings.Add(Finding(DetectorRuleIds.GenericHighEntropy, SecretKind.GenericSecret, Confidence.Suggestion, new SecretLocation(path, index, length), "High-entropy string", token));
                }
            }

            index = Math.Max(end, index + 1);
        }

        return findings;
    }

    private static List<Hit> ScanMaFile(string text, string path)
    {
        var findings = new List<Hit>();
        if (!PatternRetry.TryRun(() => (Shared: SecretPatterns.MaFileSharedSecret().Match(text), Revocation: SecretPatterns.MaFileRevocationCode().Match(text)), out var matches))
        {
            findings.Add(TimeoutFinding(DetectorRuleIds.SteamMaFile, path, 0, text.Length));
            return findings;
        }

        if (matches.Shared.Success && matches.Shared.Groups["secret"] is { } secret)
        {
            findings.Add(Finding(MaFileSharedSecretRule, SecretKind.Totp, Confidence.High, new SecretLocation(path, secret.Index, secret.Length), "Steam Guard shared secret", secret.Value));
        }

        if (matches.Revocation.Success && matches.Revocation.Groups["secret"] is { } code)
        {
            findings.Add(Finding(DetectorRuleIds.SteamMaFileRevocationCode, SecretKind.RecoveryCodes, Confidence.High, new SecretLocation(path, code.Index, code.Length), "Steam Guard revocation code", code.Value));
        }

        return findings;
    }

    private static List<Hit> Deduplicate(List<Hit> raw)
    {
        List<Hit> ordered = [.. raw
            .OrderByDescending(finding => finding.Kind == SecretKind.NeedsReview)
            .ThenByDescending(finding => finding.Confidence)
            .ThenByDescending(finding => Priority(finding.RuleId))
            .ThenByDescending(finding => finding.Location.Length)];
        var kept = new List<Hit>();
        var index = new OverlapIndex();
        foreach (Hit finding in ordered)
        {
            // Kept locations never overlap each other, so a kept finding with the same start and length as this one is
            // the only kept finding it overlaps.
            int overlap = index.FindOverlap(finding.Location);
            if (overlap < 0)
            {
                index.Add(finding.Location);
                kept.Add(finding);
                continue;
            }

            // A catalog match that is exactly the value of a labeled line takes over the line and its label, so placing it
            // removes the line and names the field after the label, which is never lost.
            Hit winner = kept[overlap];
            if (winner.Location.FirstLine is null && finding.Location.FirstLine is not null && finding.LabelLocation is not null
                && winner.Location.Start == finding.Location.Start && winner.Location.Length == finding.Location.Length)
            {
                kept[overlap] = winner with
                {
                    Location = finding.Location,
                    LabelLocation = finding.LabelLocation
                };
            }
        }

        return [.. kept.OrderBy(finding => finding.Location.Start)];
    }

    private static int Priority(string ruleId)
    {
        return ruleId switch
        {
            DetectorRuleIds.GenericHighEntropy => 0,
            DetectorRuleIds.SecurityQuestion or DetectorRuleIds.BitwardenRecoveryCode => 3,
            _ when ruleId.StartsWith("notes-", StringComparison.Ordinal) => 1,
            _ => 2,
        };
    }

    // Zero-based index of the line that holds an offset, found by binary search over the line starts.
    private static int LineIndex(List<(int Start, string Text)> lines, int offset)
    {
        int low = 0;
        int high = lines.Count - 1;
        while (low < high)
        {
            int middle = low + ((high - low + 1) / 2);
            if (lines[middle].Start <= offset)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return low;
    }

    private static int LineNumber(List<(int Start, string Text)> lines, int lineStart) => LineIndex(lines, lineStart) + 1;

    // A pattern that timed out twice in a row flags the text it ran on for review; the text is never dropped.
    private static Hit TimeoutFinding(string ruleId, string path, int start, int length, string message = "Pattern timed out; review this text manually")
    {
        return new Hit
        {
            RuleId = ruleId,
            Kind = SecretKind.NeedsReview,
            Confidence = Confidence.Suggestion,
            Location = new SecretLocation(path, start, Math.Max(1, length)),
            NeedsReview = true,
            TimedOut = true,
            Message = message,
        };
    }

    private static Hit Finding(string ruleId, SecretKind kind, Confidence confidence, SecretLocation location, string message, string value)
    {
        return new Hit
        {
            RuleId = ruleId,
            Kind = kind,
            Confidence = confidence,
            Location = location,
            Message = message,
            Value = value,
        };
    }
}
