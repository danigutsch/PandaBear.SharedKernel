using System.Collections.Frozen;
using System.Globalization;
using PandaBear.SecretScanning;
using PandaBear.Vault.Formats;
using PandaBear.Vault.Model;

namespace PandaBear.Vault.Secrets;

/// <summary>
/// Scans vault items with <see cref="SecretScanner"/> and proposes where each secret belongs in Bitwarden: the login's
/// TOTP, password, or user name, a hidden or text field, a new SSH key or card item, or nowhere. It adds the checks that
/// need the item: misfiled core fields, custom fields that already hold their secret, and the Bitwarden field budget.
/// </summary>
internal sealed class ItemScanner
{
    private static readonly string[] BankWords =
        ["bank", "card", "karte", "visa", "mastercard", "amex", "credit", "debit", "kredit", "sparkasse", "volksbank", "giro", "atm"];

    /// <summary>
    /// Where the finding of each rule or detector goes, unless the item says otherwise (see AssignTargets). Catalog rules
    /// that are not listed go where their kind suggests (DefaultTarget).
    /// </summary>
    internal static readonly FrozenDictionary<string, PlacementTarget> RuleTargets = new Dictionary<string, PlacementTarget>(StringComparer.Ordinal)
    {
        ["pgp-private-key"] = PlacementTarget.Hidden("PGP private key"),
        ["putty-private-key"] = PlacementTarget.Hidden("PuTTY private key"),
        ["gcp-service-account"] = PlacementTarget.Hidden("GCP service account"),
        ["azure-sas-url"] = PlacementTarget.Hidden("Azure SAS URL"),
        ["slack-webhook-url"] = PlacementTarget.Hidden("Slack webhook"),
        ["kubeconfig-client-key"] = PlacementTarget.Hidden("Kubernetes client key"),
        ["database-uri"] = PlacementTarget.LoginFromConnection,
        ["url-credentials"] = PlacementTarget.LoginFromConnection,
        ["connection-string"] = PlacementTarget.LoginFromConnection,
        ["jwt"] = PlacementTarget.Hidden("JWT"),
        ["otpauth-hotp"] = PlacementTarget.NotMoved("HOTP is not supported by Bitwarden"),
        ["mega-recovery-key"] = PlacementTarget.Hidden("Recovery codes"),
        ["security-question"] = PlacementTarget.HiddenNamedByLabel("Security answer"),
        ["notes-passphrase"] = PlacementTarget.Hidden("BIP39 passphrase"),
        ["notes-password"] = PlacementTarget.LoginPassword,
        ["notes-pin"] = PlacementTarget.Hidden("PIN"),
        ["notes-username"] = PlacementTarget.LoginUsername,
        ["notes-token"] = PlacementTarget.HiddenNamedByLabel("Token"),
        ["bitwarden-recovery-code"] = PlacementTarget.NotMoved("store the Bitwarden recovery code outside the vault it protects"),
        ["notes-recovery-code"] = PlacementTarget.Hidden("Recovery codes"),
        ["notes-seed"] = PlacementTarget.Hidden("Seed phrase"),
        ["notes-totp"] = PlacementTarget.LoginTotp,
        ["bic"] = PlacementTarget.Hidden("BIC"),
        ["card-number"] = PlacementTarget.Hidden("Card number"),
        ["iban"] = PlacementTarget.Hidden("IBAN"),
        ["generic-high-entropy"] = PlacementTarget.Hidden("Secret"),
        [SecretScanner.MaFileSharedSecretRule] = PlacementTarget.LoginTotp,
        ["steam-mafile-revocation-code"] = PlacementTarget.Hidden("Steam revocation code"),
        ["bip39-seed"] = PlacementTarget.Hidden("Seed phrase"),
        ["electrum-seed"] = PlacementTarget.Hidden("Seed phrase"),
        ["recovery-code-block"] = PlacementTarget.Hidden("Recovery codes"),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private readonly SecretScanner scanner;

    /// <summary>Creates an item scanner over a text scanner.</summary>
    /// <param name="scanner">The text scanner.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scanner"/> is <see langword="null"/>.</exception>
    public ItemScanner(SecretScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        this.scanner = scanner;
    }

    /// <summary>Gets an item scanner over the built-in catalog.</summary>
    public static ItemScanner Default { get; } = new(SecretScanner.Default);

    /// <summary>Scans notes, custom fields, and core login fields of an item.</summary>
    /// <param name="item">Item.</param>
    /// <returns>Findings with proposed targets.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<SecretFinding> Scan(VaultItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var context = new ItemContext(item);
        var findings = new List<SecretFinding>();
        if (!string.IsNullOrEmpty(item.Notes))
        {
            findings.AddRange(ScanField(item.Notes, ItemFieldPaths.Notes, context));
        }

        if (item.Login is { } login)
        {
            findings.AddRange(ScanCoreField(login.Password, ItemFieldPaths.LoginPassword, context));
            findings.AddRange(ScanCoreField(login.Username, ItemFieldPaths.LoginUsername, context));
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
        var context = new ItemContext(item);
        var findings = new List<SecretFinding>(scanner.Scan(text, fieldPath, context.ForScanner(fileName), Project));
        return AssignTargets(findings, context);
    }

    private List<SecretFinding> ScanCustomField(CustomField field, int index, ItemContext context)
    {
        var findings = new List<SecretFinding>();
        string? value = field.Value;
        if (string.IsNullOrEmpty(value) || field.Type is CustomFieldType.Boolean or CustomFieldType.Linked)
        {
            return findings;
        }

        string path = ItemFieldPaths.FieldValue(index);
        var label = new SecretLocation(ItemFieldPaths.FieldName(index), 0, field.Name?.Length ?? 0);
        foreach (SecretFinding finding in ScanField(value, path, context))
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

    private List<SecretFinding> ScanCoreField(string? value, string path, ItemContext context)
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

        if (path != ItemFieldPaths.LoginPassword)
        {
            return findings;
        }

        if (context.IsBankOrCard && trimmed.Length is >= 4 and <= 8 && trimmed.All(char.IsAsciiDigit))
        {
            findings.Add(Finding("misfiled-pin", SecretKind.Pin, Confidence.Medium, whole, PlacementTarget.Hidden("PIN"), "PIN stored in the password of a bank or card item", trimmed));
            return findings;
        }

        foreach (SecretFinding finding in scanner.Scan(value, path, context.ForScanner(null, catalogRulesOnly: true), Project))
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

    private List<SecretFinding> ScanField(string text, string path, ItemContext context) =>
        [.. scanner.Scan(text, path, context.ForScanner(null), Project)];

    /// <summary>
    /// Turns a finding of the text scanner into a vault finding with its proposed target. The value is seen here once and
    /// not kept: it decides whether a private key can become an SSH key item and whether it fits a Bitwarden field.
    /// </summary>
    /// <param name="detected">The finding of the text scanner.</param>
    /// <param name="value">The found value, or <see langword="null"/> when the finding spans text that is not one value.</param>
    /// <returns>The vault finding.</returns>
    internal static SecretFinding Project(DetectedSecret detected, string? value)
    {
        PlacementTarget target = detected.TimedOut
            ? PlacementTarget.NotMoved("pattern timed out")
            : RuleTargets.GetValueOrDefault(detected.RuleId) ?? DefaultTarget(detected.Kind, detected.RuleName ?? "Secret");
        bool review = detected.NeedsReview;
        string message = detected.Message;
        if (!detected.TimedOut && detected.Kind == SecretKind.PrivateKey && !RuleTargets.ContainsKey(detected.RuleId) && value is not null)
        {
            SshKeyInspection inspection = BitwardenSshKeys.Inspect(value);
            if (!inspection.CanConvert)
            {
                target = PlacementTarget.Hidden("Private key");
                review = true;
                message += $" ({inspection.Problem})";
            }
        }

        if (value is not null && !Fits(value))
        {
            review = true;
            message += "; exceeds the Bitwarden field budget";
        }

        return new SecretFinding
        {
            RuleId = detected.RuleId,
            Kind = detected.Kind,
            Confidence = detected.Confidence,
            Location = detected.Location,
            LabelLocation = detected.LabelLocation,
            ProposedTarget = target,
            NeedsReview = review,
            Message = message,
        };
    }

    private static List<SecretFinding> AssignTargets(List<SecretFinding> findings, ItemContext context)
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
                    && finding.Location.FieldPath != ItemFieldPaths.LoginPassword && !labeled && IsDefaultTokenTarget(finding):
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

    private static bool Fits(string value)
    {
        return BitwardenLimits.Fits(value, BitwardenLimits.ValueEncryptedLength);
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
    private sealed class ItemContext
    {
        public ItemContext(VaultItem? item)
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

        public bool IsLogin { get; }

        public bool IsSecureNote { get; }

        public bool HasPassword { get; }

        public bool HasUsername { get; }

        public bool HasTotp { get; }

        public string? Host { get; }

        public bool IsBitwarden { get; }

        public bool IsBankOrCard { get; }

        public SecretScanContext ForScanner(string? fileName, bool catalogRulesOnly = false) => new()
        {
            FileName = fileName,
            Host = Host,
            ExpectsUsername = IsLogin && !HasUsername,
            IsBitwardenAccount = IsBitwarden,
            CatalogRulesOnly = catalogRulesOnly,
        };

        private static string? HostOf(string value)
        {
            string candidate = value.Contains("://", StringComparison.Ordinal) ? value : "https://" + value;
            return Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) ? uri.Host : null;
        }
    }
}
