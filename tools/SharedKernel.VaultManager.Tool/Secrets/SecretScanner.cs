using System.Buffers;
using System.Globalization;
using System.Text.RegularExpressions;
using PandaBear.BuildingBlocks;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>
/// Finds secrets in vault items and attachment text. Catalog rules run only on bounded windows around keyword hits;
/// structural detectors cover labeled notes lines, security questions, recovery code blocks, card numbers, IBANs, seed
/// phrases, and misfiled core fields. Findings hold locations, never values.
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
internal sealed class SecretScanner
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

    private static readonly string[] BankWords =
        ["bank", "card", "karte", "visa", "mastercard", "amex", "credit", "debit", "kredit", "sparkasse", "volksbank", "giro", "atm"];

    private static readonly string[] QuestionContextWords = ["security question", "secret question", "sicherheitsfrage", "pregunta de seguridad"];

    private readonly IReadOnlyList<SecretRule> rules;
    private readonly SearchValues<string> keywords;

    /// <summary>Creates a scanner over a rule set.</summary>
    /// <param name="rules">Catalog rules, in priority order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rules"/> is <see langword="null"/>.</exception>
    public SecretScanner(IReadOnlyList<SecretRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        this.rules = rules;
        keywords = SearchValues.Create([.. rules.SelectMany(rule => rule.Keywords).Distinct(StringComparer.OrdinalIgnoreCase)], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Scanner over the built-in catalog.</summary>
    public static SecretScanner Default { get; } = new(SecretRuleCatalog.All);

    /// <summary>Scans notes, custom fields, and core login fields of an item.</summary>
    /// <param name="item">Item.</param>
    /// <returns>Findings with proposed targets.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<SecretFinding> Scan(VaultItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var context = new ScanContext(item);
        var findings = new List<SecretFinding>();
        if (!string.IsNullOrEmpty(item.Notes))
        {
            findings.AddRange(ScanField(item.Notes, SecretLocation.Notes, context, fileNameHint: false));
        }

        if (item.Login is { } login)
        {
            findings.AddRange(ScanCoreField(login.Password, SecretLocation.LoginPassword, context));
            findings.AddRange(ScanCoreField(login.Username, SecretLocation.LoginUsername, context));
        }

        for (int index = 0; index < item.Fields.Count; index++)
        {
            findings.AddRange(ScanCustomField(item.Fields[index], index, context));
        }

        return AssignTargets(findings, context);
    }

    /// <summary>Scans attachment text.</summary>
    /// <param name="text">Attachment text.</param>
    /// <param name="fieldPath">Field path, normally <c>attachment:&lt;file name&gt;</c>.</param>
    /// <param name="fileName">File name, whose shape can raise confidence.</param>
    /// <param name="item">Item the attachment belongs to, used for placement context.</param>
    /// <returns>Findings with proposed targets.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="fieldPath"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<SecretFinding> ScanText(string text, string fieldPath, string? fileName, VaultItem? item = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(fieldPath);
        var context = new ScanContext(item);
        var findings = new List<SecretFinding>();
        if (fileName is not null && fileName.EndsWith(".maFile", StringComparison.OrdinalIgnoreCase))
        {
            findings.AddRange(ScanMaFile(text, fieldPath));
        }

        if (fileName is not null && fileName.Equals("MEGA-RECOVERYKEY.txt", StringComparison.OrdinalIgnoreCase))
        {
            string trimmed = text.Trim();
            if (trimmed.Length is >= 16 and <= 128 && !SecretText.HasWhitespace(trimmed))
            {
                findings.Add(Finding("mega-recovery-key", SecretKind.RecoveryCodes, Confidence.High, new SecretLocation(fieldPath, text.IndexOf(trimmed, StringComparison.Ordinal), trimmed.Length), PlacementTarget.Hidden("Recovery codes"), "MEGA recovery key", trimmed));
            }
        }

        findings.AddRange(ScanField(text, fieldPath, context, IsRecoveryFileName(fileName)));
        return AssignTargets(findings, context);
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

    private List<SecretFinding> ScanCustomField(CustomField field, int index, ScanContext context)
    {
        var findings = new List<SecretFinding>();
        string? value = field.Value;
        if (string.IsNullOrEmpty(value) || field.Type is CustomFieldType.Boolean or CustomFieldType.Linked)
        {
            return findings;
        }

        string path = SecretLocation.FieldValue(index);
        var label = new SecretLocation(SecretLocation.FieldName(index), 0, field.Name?.Length ?? 0);
        foreach (SecretFinding finding in ScanField(value, path, context, fileNameHint: false))
        {
            // A secret that fills a hidden field is reported only when it has a better place: the TOTP slot, an SSH key
            // item, login fields, or outside the vault. A private key that cannot become an SSH key item would only move
            // to a hidden field again.
            bool whole = value.Trim().Length == finding.Location.Length;
            bool hasBetterPlace = finding.Kind is SecretKind.Totp or SecretKind.BitwardenRecoveryCode or SecretKind.ConnectionString
                || (finding.Kind == SecretKind.PrivateKey && finding.ProposedTarget.Kind != PlacementTargetKind.HiddenField);
            if (field.Type == CustomFieldType.Hidden && whole && !hasBetterPlace)
            {
                continue;
            }

            findings.Add(whole && field.Name is { Length: > 0 } ? finding with
            {
                LabelLocation = label
            } : finding);
        }

        return findings;
    }

    private List<SecretFinding> ScanCoreField(string? value, string path, ScanContext context)
    {
        var findings = new List<SecretFinding>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return findings;
        }

        string trimmed = value.Trim();
        var whole = new SecretLocation(path, value.IndexOf(trimmed, StringComparison.Ordinal), trimmed.Length);
        if (trimmed.StartsWith("otpauth://totp/", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
        {
            if (TotpUri.TryNormalize(trimmed, null, null, out _))
            {
                findings.Add(Finding("misfiled-totp", SecretKind.Totp, Confidence.High, whole, PlacementTarget.LoginTotp, $"TOTP URI stored in {path}", trimmed));
            }

            return findings;
        }

        if (LooksLikeBase32Seed(trimmed))
        {
            findings.Add(Finding("misfiled-totp", SecretKind.Totp, Confidence.Medium, whole, PlacementTarget.LoginTotp, $"TOTP seed stored in {path}", trimmed));
            return findings;
        }

        if (path != SecretLocation.LoginPassword)
        {
            return findings;
        }

        if (context.IsBankOrCard && trimmed.Length is >= 4 and <= 8 && trimmed.All(char.IsAsciiDigit))
        {
            findings.Add(Finding("misfiled-pin", SecretKind.Pin, Confidence.Medium, whole, PlacementTarget.Hidden("PIN"), "PIN stored in the password of a bank or card item", trimmed));
            return findings;
        }

        foreach (SecretFinding finding in ScanCatalog(value, path))
        {
            if (finding.Kind is SecretKind.PrivateKey or SecretKind.PgpPrivateKey or SecretKind.PuttyKey)
            {
                findings.Add(finding with
                {
                    Message = finding.Message + " stored in the password field"
                });
            }
        }

        if (findings.Count == 0 && trimmed.Length > 256 && !SecretText.HasWhitespace(trimmed))
        {
            findings.Add(Finding("long-token-in-password", SecretKind.GenericSecret, Confidence.Suggestion, whole, PlacementTarget.NotMoved("long token stored as the password"), "Long token stored in the password field", trimmed) with
            {
                NeedsReview = true
            });
        }

        return findings;
    }

    private List<SecretFinding> ScanField(string text, string path, ScanContext context, bool fileNameHint)
    {
        List<SecretFinding> seeds = ScanSeedPhrases(text, path);
        var raw = new List<SecretFinding>();
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

    private List<SecretFinding> ScanCatalog(string text, string path)
    {
        var findings = new List<SecretFinding>();
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

                foreach (SecretFinding finding in matched)
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

    private static List<SecretFinding> MatchWindow(SecretRule rule, string text, string path, int start, int end)
    {
        var findings = new List<SecretFinding>();
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

    private static SecretFinding? ToFinding(SecretRule rule, string text, string path, Match match, int windowStart, int windowEnd)
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

        PlacementTarget target = rule.Target ?? DefaultTarget(rule.Kind, rule.DisplayName);
        bool review = false;
        if (rule.Kind == SecretKind.PrivateKey && rule.Target is null)
        {
            SshKeyInspection inspection = SshKeyConverter.Inspect(value);
            if (!inspection.CanConvert)
            {
                target = PlacementTarget.Hidden("Private key");
                review = true;
                message += $" ({inspection.Problem})";
            }
        }
        else if (rule.Kind is SecretKind.PuttyKey || rule.Id == "otpauth-hotp")
        {
            review = true;
        }

        return Finding(rule.Id, rule.Kind, confidence, new SecretLocation(path, start, end - start), target, message, value) with
        {
            NeedsReview = review || !Fits(value)
        };
    }

    private List<SecretFinding> ScanAwsContext(string text, string path, List<SecretFinding> existing)
    {
        var findings = new List<SecretFinding>();
        SecretRule? rule = rules.FirstOrDefault(candidate => candidate.Id == "aws-secret-access-key");
        var keyIds = existing.Where(finding => finding.RuleId == "aws-access-key-id").ToList();
        if (rule is null || keyIds.Count == 0)
        {
            return findings;
        }

        var lines = SecretText.Lines(text);
        var scanned = new HashSet<int>();
        foreach (SecretFinding keyId in keyIds)
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

    private static List<SecretFinding> AwsSecretCandidates(SecretRule rule, string lineText, int lineStart, string path)
    {
        var findings = new List<SecretFinding>();
        for (Match match = SecretPatterns.AwsSecretCandidate().Match(lineText); match.Success; match = match.NextMatch())
        {
            if (SecretText.ShannonEntropy(match.Value) >= rule.MinimumEntropy && SecretText.CharacterClasses(match.Value) >= 3)
            {
                var location = new SecretLocation(path, lineStart + match.Index, match.Length);
                findings.Add(Finding(rule.Id, rule.Kind, Confidence.Medium, location, DefaultTarget(rule.Kind, rule.DisplayName), rule.DisplayName + " near an access key ID", match.Value));
            }
        }

        return findings;
    }

    // Lines that hold only whitespace never hold a finding, so the scan steps over them; "next" is the following line that
    // holds text.
    private static List<SecretFinding> ScanLines(string text, string path, ScanContext context, bool seedNearby)
    {
        var findings = new List<SecretFinding>();
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
                findings.Add(TimeoutFinding("notes-line", path, lines[index].Start, line.Length));
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
            if (trimmed.EndsWith('?') && trimmed.Length is > 3 and <= 120 && next >= 0 && parsed[next] is null or { Class: KeyClass.None })
            {
                string answerLine = lines[next].Text.Trim();
                if (answerLine.Length is > 0 and <= 100 && !answerLine.EndsWith('?'))
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

    private static SecretFinding QuestionFinding(string path, int questionStart, int questionLength, int answerStart, string answer, int questionLine, int answerLine, Confidence confidence)
    {
        var location = new SecretLocation(path, answerStart, answer.Length, questionLine, answerLine);
        SecretFinding finding = Finding("security-question", SecretKind.SecurityQuestion, confidence, location, PlacementTarget.HiddenNamedByLabel("Security answer"), "Security question and answer", answer);
        return finding with
        {
            LabelLocation = new SecretLocation(path, questionStart, questionLength)
        };
    }

    private static SecretFinding? LineFinding(string key, int keyStart, KeyClass keyClass, int start, string value, int line, string path, ScanContext context, bool seedNearby)
    {
        var location = new SecretLocation(path, start, value.Length, line, line);
        bool rejected = CandidateFilter.IsRejected(value);
        string normalizedKey = KeyClassifier.Normalize(key);
        switch (keyClass)
        {
            case KeyClass.Password when !rejected:
                bool bip39Passphrase = seedNearby && normalizedKey is "passphrase" or "bip39passphrase";
                return Finding(bip39Passphrase ? "notes-passphrase" : "notes-password", SecretKind.Password, Confidence.Medium, location, bip39Passphrase ? PlacementTarget.Hidden("BIP39 passphrase") : PlacementTarget.LoginPassword, "Password line", value);
            case KeyClass.Pin when value.Length is >= 3 and <= 12 && !SecretText.HasWhitespace(value) && !CandidateFilter.IsPlaceholder(value):
                return Finding("notes-pin", SecretKind.Pin, Confidence.Medium, location, PlacementTarget.Hidden("PIN"), "PIN line", value);
            case KeyClass.Username when context.IsLogin && !context.HasUsername && !CandidateFilter.IsPlaceholder(value) && value.Length <= 256:
                return Finding("notes-username", SecretKind.Username, Confidence.Medium, location, PlacementTarget.LoginUsername, "User name line", value);
            case KeyClass.Token when !rejected && value.Length is >= 8 and <= 512 && !SecretText.HasWhitespace(value) && SecretText.ShannonEntropy(value) >= 3.0:
                var label = new SecretLocation(path, keyStart, key.Length);
                return Finding("notes-token", SecretKind.ApiToken, Confidence.Low, location, PlacementTarget.HiddenNamedByLabel("Token"), "Token line", value) with
                {
                    LabelLocation = label
                };
            case KeyClass.Recovery when context.IsBitwarden && IsBitwardenRecoveryCode(value):
                return Finding("bitwarden-recovery-code", SecretKind.BitwardenRecoveryCode, Confidence.Medium, location, PlacementTarget.NotMoved("store the Bitwarden recovery code outside the vault it protects"), "Bitwarden account recovery code", value) with
                {
                    NeedsReview = true
                };
            case KeyClass.Recovery when !rejected && value.Length >= 8:
                return Finding("notes-recovery-code", SecretKind.RecoveryCodes, Confidence.Medium, location, PlacementTarget.Hidden("Recovery codes"), "Recovery code line", value);
            case KeyClass.Seed when value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 12:
                return Finding("notes-seed", SecretKind.SeedPhrase, Confidence.Low, location, PlacementTarget.Hidden("Seed phrase"), "Seed phrase line", value);
            case KeyClass.Totp when TotpUri.TryNormalize(value, null, null, out _):
                return Finding("notes-totp", SecretKind.Totp, Confidence.Medium, location, PlacementTarget.LoginTotp, "TOTP secret line", value);
            case KeyClass.Bic when Bic.Create(value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant()).IsSuccess:
                return Finding("bic", SecretKind.Iban, Confidence.Medium, location, PlacementTarget.Hidden("BIC"), "BIC line", value);
            default:
                return null;
        }
    }

    private static bool IsBitwardenRecoveryCode(string value)
    {
        string compact = value.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        return compact.Length == 32 && compact.All(character => char.IsAsciiDigit(character) || char.IsAsciiLetterLower(character));
    }

    private static List<SecretFinding> ScanCardNumbers(string text, string path)
    {
        var findings = new List<SecretFinding>();
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
                findings.Add(Finding("card-number", SecretKind.CardNumber, Confidence.High, new SecretLocation(path, index, end - index), PlacementTarget.Hidden("Card number"), brand + " card number", number));
            }

            index = Math.Max(end, index + 1);
        }

        return findings;
    }

    private static List<SecretFinding> ScanIbans(string text, string path)
    {
        var findings = new List<SecretFinding>();
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
                findings.Add(Finding("iban", SecretKind.Iban, Confidence.High, new SecretLocation(path, index, bestEnd - index), PlacementTarget.Hidden("IBAN"), "IBAN", text[index..bestEnd]));
                index = bestEnd;
            }
        }

        return findings;
    }

    private static List<SecretFinding> ScanSeedPhrases(string text, string path)
    {
        var findings = new List<SecretFinding>();
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

    private static void CheckRun(List<(string Word, int Start, int End)> run, string path, List<SecretFinding> findings)
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

            Add(run.Skip(offset).Take(matched).ToList(), "bip39-seed", "BIP39 seed phrase");
            found = true;
            offset += matched;
        }

        if (!found && run.Count is 12 or 13 or 24 && Bip39.IsElectrumSeed([.. run.Select(entry => entry.Word)]))
        {
            Add(run, "electrum-seed", "Electrum seed phrase");
        }

        void Add(List<(string Word, int Start, int End)> words, string ruleId, string message)
        {
            int start = words[0].Start;
            int end = words[^1].End;
            findings.Add(new SecretFinding
            {
                RuleId = ruleId,
                Kind = SecretKind.SeedPhrase,
                Confidence = Confidence.High,
                Location = new SecretLocation(path, start, end - start),
                ProposedTarget = PlacementTarget.Hidden("Seed phrase"),
                Message = string.Create(CultureInfo.InvariantCulture, $"{message} ({words.Count} words)"),
            });
        }
    }

    private static List<SecretFinding> ScanHighEntropy(string text, string path)
    {
        var findings = new List<SecretFinding>();
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
                    findings.Add(Finding("generic-high-entropy", SecretKind.GenericSecret, Confidence.Suggestion, new SecretLocation(path, index, length), PlacementTarget.Hidden("Secret"), "High-entropy string", token));
                }
            }

            index = Math.Max(end, index + 1);
        }

        return findings;
    }

    private static List<SecretFinding> ScanMaFile(string text, string path)
    {
        var findings = new List<SecretFinding>();
        if (!PatternRetry.TryRun(() => (Shared: SecretPatterns.MaFileSharedSecret().Match(text), Revocation: SecretPatterns.MaFileRevocationCode().Match(text)), out var matches))
        {
            findings.Add(TimeoutFinding("steam-mafile", path, 0, text.Length));
            return findings;
        }

        if (matches.Shared.Success && matches.Shared.Groups["secret"] is { } secret)
        {
            findings.Add(Finding(MaFileSharedSecretRule, SecretKind.Totp, Confidence.High, new SecretLocation(path, secret.Index, secret.Length), PlacementTarget.LoginTotp, "Steam Guard shared secret", secret.Value));
        }

        if (matches.Revocation.Success && matches.Revocation.Groups["secret"] is { } code)
        {
            findings.Add(Finding("steam-mafile-revocation-code", SecretKind.RecoveryCodes, Confidence.High, new SecretLocation(path, code.Index, code.Length), PlacementTarget.Hidden("Steam revocation code"), "Steam Guard revocation code", code.Value));
        }

        return findings;
    }

    private static List<SecretFinding> Deduplicate(List<SecretFinding> raw)
    {
        List<SecretFinding> ordered = [.. raw
            .OrderByDescending(finding => finding.Kind == SecretKind.NeedsReview)
            .ThenByDescending(finding => finding.Confidence)
            .ThenByDescending(finding => Priority(finding.RuleId))
            .ThenByDescending(finding => finding.Location.Length)];
        var kept = new List<SecretFinding>();
        var index = new OverlapIndex();
        foreach (SecretFinding finding in ordered)
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
            SecretFinding winner = kept[overlap];
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
            "generic-high-entropy" => 0,
            "security-question" or "bitwarden-recovery-code" => 3,
            _ when ruleId.StartsWith("notes-", StringComparison.Ordinal) => 1,
            _ => 2,
        };
    }

    private static List<SecretFinding> AssignTargets(List<SecretFinding> findings, ScanContext context)
    {
        bool totpFilled = context.HasTotp;
        bool passwordFilled = context.HasPassword;
        bool usernameFilled = context.HasUsername;
        int extraTotp = 1;
        var result = new List<SecretFinding>(findings.Count);
        foreach (SecretFinding finding in findings)
        {
            PlacementTarget target = finding.ProposedTarget;
            // A labeled value, from a named custom field or a labeled line, keeps its label as the name of its field.
            bool labeled = finding.LabelLocation is not null && finding.Kind != SecretKind.SecurityQuestion;
            switch (finding.Kind)
            {
                case SecretKind.Totp when target.Kind is PlacementTargetKind.LoginTotp:
                    if (context.IsLogin && !totpFilled)
                    {
                        totpFilled = true;
                    }
                    else
                    {
                        extraTotp++;
                        target = PlacementTarget.Hidden(context.IsLogin || extraTotp > 2
                            ? string.Create(CultureInfo.InvariantCulture, $"TOTP ({extraTotp})")
                            : "TOTP");
                    }

                    break;
                case SecretKind.Password when target.Kind == PlacementTargetKind.LoginPassword:
                    if (context.IsLogin && !passwordFilled)
                    {
                        passwordFilled = true;
                    }
                    else
                    {
                        target = PlacementTarget.Hidden("Password (notes)");
                    }

                    break;
                case SecretKind.ApiToken or SecretKind.GenericSecret when target.Kind == PlacementTargetKind.HiddenField && finding.Confidence > Confidence.Suggestion
                    && finding.Location.FieldPath != SecretLocation.LoginPassword && !labeled && IsDefaultTokenTarget(finding):
                    if (context.IsLogin && !passwordFilled)
                    {
                        passwordFilled = true;
                        target = PlacementTarget.LoginPassword;
                    }

                    break;
                case SecretKind.AccessKeyId when target.Kind == PlacementTargetKind.TextField && !labeled:
                    if (context.IsLogin && !usernameFilled)
                    {
                        usernameFilled = true;
                        target = PlacementTarget.LoginUsername;
                    }

                    break;
                case SecretKind.Username:
                    usernameFilled = true;
                    break;
                case SecretKind.CardNumber when target.Kind == PlacementTargetKind.HiddenField && !labeled && context.IsSecureNote:
                    target = PlacementTarget.NewCardItem;
                    break;
                case SecretKind.ConnectionString or SecretKind.UrlCredentials when target.Kind == PlacementTargetKind.LoginFromConnection && !context.IsLogin:
                    target = PlacementTarget.Hidden("Connection string");
                    break;
                default:
                    break;
            }

            if (labeled && target.Kind == PlacementTargetKind.HiddenField && !target.NameFromLabel)
            {
                target = PlacementTarget.HiddenNamedByLabel(target.FieldName ?? "Secret");
            }

            result.Add(finding with
            {
                ProposedTarget = target
            });
        }

        return result;
    }

    private static bool IsDefaultTokenTarget(SecretFinding finding)
    {
        return finding.RuleId is not ("gcp-service-account" or "onepassword-secret-key" or "wireguard-key" or "age-secret-key" or "kubeconfig-client-key" or "jwt");
    }

    private static PlacementTarget DefaultTarget(SecretKind kind, string displayName)
    {
        return kind switch
        {
            SecretKind.Totp => PlacementTarget.LoginTotp,
            SecretKind.AccessKeyId => PlacementTarget.Text(displayName),
            SecretKind.PrivateKey => PlacementTarget.NewSshKeyItem,
            SecretKind.ConnectionString => PlacementTarget.LoginFromConnection,
            _ => PlacementTarget.Hidden(displayName),
        };
    }

    private static bool LooksLikeBase32Seed(string value)
    {
        string compact = value.Replace(" ", string.Empty, StringComparison.Ordinal).TrimEnd('=');
        return compact.Length is >= 16 and <= 128
            && compact.All(character => character is (>= 'A' and <= 'Z') or (>= '2' and <= '7'))
            && compact.Any(character => character is >= '2' and <= '7')
            && compact.Distinct().Count() >= 8
            && SecretText.ShannonEntropy(compact) >= 3.0;
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

    private static bool Fits(string value)
    {
        return BitwardenLimits.Fits(value, BitwardenLimits.ValueEncryptedLength);
    }

    // A pattern that timed out twice in a row flags the text it ran on for review; the text is never dropped.
    private static SecretFinding TimeoutFinding(string ruleId, string path, int start, int length, string message = "Pattern timed out; review this text manually")
    {
        return new SecretFinding
        {
            RuleId = ruleId,
            Kind = SecretKind.NeedsReview,
            Confidence = Confidence.Suggestion,
            Location = new SecretLocation(path, start, Math.Max(1, length)),
            ProposedTarget = PlacementTarget.NotMoved("pattern timed out"),
            NeedsReview = true,
            Message = message,
        };
    }

    private static SecretFinding Finding(string ruleId, SecretKind kind, Confidence confidence, SecretLocation location, PlacementTarget target, string message, string value)
    {
        bool fits = Fits(value);
        return new SecretFinding
        {
            RuleId = ruleId,
            Kind = kind,
            Confidence = confidence,
            Location = location,
            ProposedTarget = target,
            NeedsReview = !fits,
            Message = fits ? message : message + "; exceeds the Bitwarden field budget",
        };
    }

    /// <summary>Facts about the scanned item that steer placement.</summary>
    private sealed class ScanContext
    {
        public ScanContext(VaultItem? item)
        {
            IsLogin = item?.Type == VaultItemType.Login;
            IsSecureNote = item?.Type == VaultItemType.SecureNote;
            HasPassword = !string.IsNullOrEmpty(item?.Login?.Password);
            HasUsername = !string.IsNullOrEmpty(item?.Login?.Username);
            HasTotp = !string.IsNullOrEmpty(item?.Login?.Totp);
            Host = item?.Login?.Uris.Select(uri => HostOf(uri.Value)).FirstOrDefault(host => host is not null);
            string identity = string.Join(' ', item?.Name, item?.FolderPath, Host);
            IsBitwarden = identity.Contains("bitwarden", StringComparison.OrdinalIgnoreCase) || identity.Contains("vaultwarden", StringComparison.OrdinalIgnoreCase);
            IsBankOrCard = item is not null && (item.Type == VaultItemType.Card || item.Card is not null || BankWords.Any(word => identity.Contains(word, StringComparison.OrdinalIgnoreCase)));
        }

        public bool IsLogin
        {
            get;
        }

        public bool IsSecureNote
        {
            get;
        }

        public bool HasPassword
        {
            get;
        }

        public bool HasUsername
        {
            get;
        }

        public bool HasTotp
        {
            get;
        }

        public string? Host
        {
            get;
        }

        public bool IsBitwarden
        {
            get;
        }

        public bool IsBankOrCard
        {
            get;
        }

        private static string? HostOf(string value)
        {
            string candidate = value.Contains("://", StringComparison.Ordinal) ? value : "https://" + value;
            return Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) ? uri.Host : null;
        }
    }
}
