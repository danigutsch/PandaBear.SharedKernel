namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>
/// Catalog of prefix and structure rules. Rules are rewritten in .NET syntax from gitleaks (MIT, Zachary Rice) and
/// Betterleaks (MIT) at the commits named in <see cref="SecretRule.Source"/>, or from vendor documentation.
/// </summary>
internal static class SecretRuleCatalog
{
    private const string Gitleaks = "gitleaks@09242ce9c8a60d9b051fc2d166f9e849b88c7ac0 ";
    private const string Betterleaks = "betterleaks@c613797a684337a014168063577bb6fa6e3b6296 ";
    private const int MaxStructureLength = 16_384;

    /// <summary>All rules, in priority order: earlier rules win ties between overlapping matches.</summary>
    public static IReadOnlyList<SecretRule> All { get; } = Build();

    private static List<SecretRule> Build()
    {
        return
        [
            // Keys and key files: multi-line structures found by header plus end marker.
            new()
            {
                Id = "pem-private-key", DisplayName = "Private key", Kind = SecretKind.PrivateKey, Confidence = Confidence.High,
                Keywords = ["PRIVATE KEY-----"], Pattern = SecretPatterns.PemPrivateKeyHeader(),
                Extent = static (text, start) => EndAfter(text, start, "-----END ", "PRIVATE KEY-----"),
                Validator = static value => PemBlock.TryParse(value, out PemBlock? block) && block is not null && SecretText.TryDecodeBase64(block.Body, out _),
                Source = Gitleaks + "private-key; PEM structure per RFC 7468",
            },
            new()
            {
                Id = "ssh2-encrypted-private-key", DisplayName = "Private key", Kind = SecretKind.PrivateKey, Confidence = Confidence.High,
                Keywords = ["BEGIN SSH2 ENCRYPTED PRIVATE KEY"], Pattern = SecretPatterns.Ssh2EncryptedHeader(),
                Extent = static (text, start) => EndAfter(text, start, "---- END SSH2 ENCRYPTED PRIVATE KEY ----", string.Empty),
                Source = "RFC 4716 / ssh.com key file format",
            },
            new()
            {
                Id = "pgp-private-key", DisplayName = "PGP private key", Kind = SecretKind.PgpPrivateKey, Confidence = Confidence.High,
                Keywords = ["BEGIN PGP PRIVATE KEY BLOCK"], Pattern = SecretPatterns.PgpPrivateKeyHeader(),
                Extent = static (text, start) => EndAfter(text, start, "-----END PGP PRIVATE KEY BLOCK-----", string.Empty),
                Target = PlacementTarget.Hidden("PGP private key"),
                Source = "RFC 4880 section 6.2 armor",
            },
            new()
            {
                Id = "putty-private-key", DisplayName = "PuTTY private key", Kind = SecretKind.PuttyKey, Confidence = Confidence.High,
                Keywords = ["PuTTY-User-Key-File-"], Pattern = SecretPatterns.PuttyHeader(),
                Extent = static (text, start) => EndOfLineAfter(text, start, "Private-MAC:"),
                Validator = static value => value.Contains("\nEncryption: none", StringComparison.Ordinal) || value.Contains("\nEncryption: aes256-cbc", StringComparison.Ordinal),
                Target = PlacementTarget.Hidden("PuTTY private key"),
                Source = "PuTTY documentation, appendix C (PPK file format)",
            },
            new()
            {
                Id = "gcp-service-account", DisplayName = "GCP service account", Kind = SecretKind.GenericSecret, Confidence = Confidence.High,
                Keywords = ["service_account"], Pattern = SecretPatterns.JsonObject(),
                Validator = static value => value.Contains("\"service_account\"", StringComparison.Ordinal)
                    && value.Contains("\"private_key\"", StringComparison.Ordinal) && value.Contains("\"client_email\"", StringComparison.Ordinal),
                Target = PlacementTarget.Hidden("GCP service account"),
                Source = Betterleaks + "gcp-service-account",
            },

            // Source control and package registries.
            new()
            {
                Id = "github-token", DisplayName = "GitHub token", Kind = SecretKind.ApiToken, Confidence = Confidence.High,
                ConfidenceWhenInvalid = Confidence.Low, Validator = TokenChecksums.IsValidGitHubToken,
                Keywords = ["ghp_", "gho_", "ghu_", "ghs_", "ghr_"], Pattern = SecretPatterns.GitHubToken(),
                Source = Gitleaks + "github-pat, github-oauth, github-app-token, github-refresh-token; checksum per GitHub token format announcement (2021)",
            },
            new()
            {
                Id = "github-fine-grained-pat", DisplayName = "GitHub token", Kind = SecretKind.ApiToken,
                Keywords = ["github_pat_"], Pattern = SecretPatterns.GitHubFineGrainedPat(), Source = Gitleaks + "github-fine-grained-pat",
            },
            new()
            {
                Id = "gitlab-pat", DisplayName = "GitLab token", Kind = SecretKind.ApiToken,
                Keywords = ["glpat-"], Pattern = SecretPatterns.GitLabPat(), Source = Gitleaks + "gitlab-pat; " + Betterleaks + "gitlab-pat-routable",
            },
            new()
            {
                Id = "gitlab-token", DisplayName = "GitLab token", Kind = SecretKind.ApiToken,
                Keywords = ["gldt-", "glrt-", "glrtr-", "glcbt-", "glptt-", "glft-", "glimt-", "glagent-", "gloas-", "glsoat-", "glffct-", "glwt-"],
                Pattern = SecretPatterns.GitLabToken(),
                Source = Betterleaks + "gitlab-deploy-token, gitlab-runner-authentication-token, gitlab-cicd-job-token, gitlab-ptt, gitlab-feed-token, gitlab-incoming-mail-token, gitlab-kubernetes-agent-token, gitlab-oauth-app-secret, gitlab-scim-token, gitlab-feature-flag-client-token; glrtr- and glwt- unverified",
            },
            new()
            {
                Id = "npm-token", DisplayName = "npm token", Kind = SecretKind.ApiToken, Confidence = Confidence.High,
                ConfidenceWhenInvalid = Confidence.Low, Validator = TokenChecksums.IsValidNpmToken,
                Keywords = ["npm_"], Pattern = SecretPatterns.NpmToken(), Source = Gitleaks + "npm-access-token; checksum layout unverified",
            },
            new()
            {
                Id = "pypi-token", DisplayName = "PyPI token", Kind = SecretKind.ApiToken,
                Keywords = ["pypi-AgEIcHlwaS5vcmc"], Pattern = SecretPatterns.PypiToken(), Source = Gitleaks + "pypi-upload-token",
            },
            new()
            {
                Id = "dockerhub-token", DisplayName = "Docker Hub token", Kind = SecretKind.ApiToken,
                Keywords = ["dckr_pat_", "dckr_oat_"], Pattern = SecretPatterns.DockerHubToken(),
                Source = Betterleaks + "dockerhub-personal-access-token, dockerhub-organization-access-token",
            },
            new()
            {
                Id = "nuget-api-key", DisplayName = "NuGet API key", Kind = SecretKind.ApiToken,
                Keywords = ["oy2"], Pattern = SecretPatterns.NuGetApiKey(), Source = Betterleaks + "nuget-api-key",
            },

            // Cloud providers.
            new()
            {
                Id = "aws-access-key-id", DisplayName = "AWS access key ID", Kind = SecretKind.AccessKeyId, IsIdentifier = true,
                Keywords = ["AKIA", "ASIA", "ABIA", "ACCA", "A3T"], Pattern = SecretPatterns.AwsAccessKeyId(), Source = Gitleaks + "aws-access-token",
            },
            new()
            {
                Id = "aws-secret-access-key", DisplayName = "AWS secret access key", Kind = SecretKind.ApiToken, MinimumEntropy = 4.0,
                Keywords = ["secret_access_key", "secretaccesskey", "secret access key"], Pattern = SecretPatterns.AwsSecretAccessKey(),
                Source = Betterleaks + "aws-secret-access-key",
            },
            new()
            {
                Id = "gcp-api-key", DisplayName = "Google API key", Kind = SecretKind.ApiToken,
                Keywords = ["AIza"], Pattern = SecretPatterns.GcpApiKey(), Source = Gitleaks + "gcp-api-key",
            },
            new()
            {
                Id = "google-oauth-client-secret", DisplayName = "Google OAuth client secret", Kind = SecretKind.ApiToken,
                Keywords = ["GOCSPX-"], Pattern = SecretPatterns.GoogleOAuthClientSecret(), Verified = false,
                Source = "Google OAuth client secret prefix (vendor format); length unverified",
            },
            new()
            {
                Id = "google-oauth-client-id", DisplayName = "Google OAuth client ID", Kind = SecretKind.AccessKeyId, IsIdentifier = true,
                Keywords = [".apps.googleusercontent.com"], Pattern = SecretPatterns.GoogleOAuthClientId(), Verified = false,
                Source = "Google OAuth client ID format (vendor documentation)",
            },
            new()
            {
                Id = "azure-storage-account-key", DisplayName = "Azure storage account key", Kind = SecretKind.ApiToken,
                Keywords = ["AccountKey="], Pattern = SecretPatterns.AzureStorageAccountKey(), Source = Betterleaks + "azure-storage-account-key",
            },
            new()
            {
                Id = "entra-client-secret", DisplayName = "Entra client secret", Kind = SecretKind.ApiToken,
                Keywords = ["Q~"], Pattern = SecretPatterns.EntraClientSecret(), Source = Gitleaks + "azure-ad-client-secret",
            },
            new()
            {
                Id = "azure-sas-url", DisplayName = "Azure SAS URL", Kind = SecretKind.UrlCredentials,
                Keywords = ["sig="], Pattern = SecretPatterns.AzureSasUrl(), Validator = IsSasUrl,
                Target = PlacementTarget.Hidden("Azure SAS URL"),
                Source = "Azure Storage SAS documentation (sv, se, sig query parameters)",
            },
            new()
            {
                Id = "digitalocean-token", DisplayName = "DigitalOcean token", Kind = SecretKind.ApiToken,
                Keywords = ["dop_v1_", "doo_v1_", "dor_v1_"], Pattern = SecretPatterns.DigitalOceanToken(),
                Source = Gitleaks + "digitalocean-pat, digitalocean-access-token, digitalocean-refresh-token",
            },
            new()
            {
                Id = "cloudflare-api-token", DisplayName = "Cloudflare API token", Kind = SecretKind.ApiToken,
                Keywords = ["cfut_", "cfat_"], Pattern = SecretPatterns.CloudflareApiToken(), Source = Betterleaks + "cloudflare-api-key.2",
            },

            // SaaS and AI APIs.
            new()
            {
                Id = "slack-bot-token", DisplayName = "Slack token", Kind = SecretKind.ApiToken,
                Keywords = ["xoxb-"], Pattern = SecretPatterns.SlackBotToken(), Source = Gitleaks + "slack-bot-token; " + Betterleaks + "slack-legacy-bot-token",
            },
            new()
            {
                Id = "slack-user-token", DisplayName = "Slack token", Kind = SecretKind.ApiToken,
                Keywords = ["xoxp-", "xoxe-"], Pattern = SecretPatterns.SlackUserToken(), Source = Gitleaks + "slack-user-token",
            },
            new()
            {
                Id = "slack-app-token", DisplayName = "Slack token", Kind = SecretKind.ApiToken,
                Keywords = ["xapp-"], Pattern = SecretPatterns.SlackAppToken(), Source = Gitleaks + "slack-app-token",
            },
            new()
            {
                Id = "slack-webhook-url", DisplayName = "Slack webhook", Kind = SecretKind.UrlCredentials,
                Keywords = ["hooks.slack.com"], Pattern = SecretPatterns.SlackWebhookUrl(), Target = PlacementTarget.Hidden("Slack webhook"),
                Source = Gitleaks + "slack-webhook-url",
            },
            new()
            {
                Id = "stripe-secret-key", DisplayName = "Stripe secret key", Kind = SecretKind.ApiToken,
                Keywords = ["sk_live_", "sk_test_", "sk_prod_", "rk_live_", "rk_test_", "rk_prod_"], Pattern = SecretPatterns.StripeSecretKey(),
                Source = Gitleaks + "stripe-access-token",
            },
            new()
            {
                Id = "stripe-webhook-secret", DisplayName = "Stripe webhook secret", Kind = SecretKind.ApiToken,
                Keywords = ["whsec_"], Pattern = SecretPatterns.StripeWebhookSecret(), Verified = false,
                Source = "Stripe webhook signing secret prefix (vendor documentation); length unverified",
            },
            new()
            {
                Id = "stripe-publishable-key", DisplayName = "Stripe publishable key", Kind = SecretKind.AccessKeyId, IsIdentifier = true,
                Keywords = ["pk_live_", "pk_test_"], Pattern = SecretPatterns.StripePublishableKey(),
                Source = "Stripe API keys documentation (publishable keys are public)",
            },
            new()
            {
                Id = "openai-api-key", DisplayName = "OpenAI API key", Kind = SecretKind.ApiToken,
                Keywords = ["T3BlbkFJ"], Pattern = SecretPatterns.OpenAiApiKey(), Source = Gitleaks + "openai-api-key",
            },
            new()
            {
                Id = "anthropic-api-key", DisplayName = "Anthropic API key", Kind = SecretKind.ApiToken,
                Keywords = ["sk-ant-api03-", "sk-ant-admin01-"], Pattern = SecretPatterns.AnthropicApiKey(),
                Source = Gitleaks + "anthropic-api-key, anthropic-admin-api-key",
            },
            new()
            {
                Id = "twilio-sid", DisplayName = "Twilio SID", Kind = SecretKind.AccessKeyId, IsIdentifier = true,
                Keywords = ["twilio"], Pattern = SecretPatterns.TwilioSid(), Source = Gitleaks + "twilio-api-key; account SID prefix AC per Twilio documentation",
            },
            new()
            {
                Id = "twilio-auth-token", DisplayName = "Twilio auth token", Kind = SecretKind.ApiToken,
                Keywords = ["twilio"], Pattern = SecretPatterns.TwilioAuthToken(), Verified = false,
                Source = "Twilio auth token format (32 hex, vendor documentation); context rule",
            },
            new()
            {
                Id = "sendgrid-api-key", DisplayName = "SendGrid API key", Kind = SecretKind.ApiToken,
                Keywords = ["SG."], Pattern = SecretPatterns.SendGridApiKey(), Source = Gitleaks + "sendgrid-api-token (split into 22.43 segments)",
            },
            new()
            {
                Id = "discord-bot-token", DisplayName = "Discord bot token", Kind = SecretKind.ApiToken,
                Keywords = ["discord"], Pattern = SecretPatterns.DiscordBotToken(), Verified = false,
                Source = "Discord bot token shape (community documentation); unverified",
            },
            new()
            {
                Id = "telegram-bot-token", DisplayName = "Telegram bot token", Kind = SecretKind.ApiToken,
                Keywords = ["telegr", "bot"], Pattern = SecretPatterns.TelegramBotToken(), Source = Gitleaks + "telegram-bot-api-token",
            },
            new()
            {
                Id = "shopify-token", DisplayName = "Shopify token", Kind = SecretKind.ApiToken,
                Keywords = ["shpat_", "shpca_", "shppa_", "shpss_"], Pattern = SecretPatterns.ShopifyToken(),
                Source = Gitleaks + "shopify-access-token; " + Betterleaks + "shopify-custom-access-token, shopify-private-app-access-token, shopify-shared-secret",
            },

            // Infrastructure and secrets tooling.
            new()
            {
                Id = "tailscale-key", DisplayName = "Tailscale key", Kind = SecretKind.ApiToken,
                Keywords = ["tskey-"], Pattern = SecretPatterns.TailscaleKey(),
                Source = Betterleaks + "tailscale-api-key.1; auth, client, scim, webhook prefixes unverified",
            },
            new()
            {
                Id = "vault-token", DisplayName = "HashiCorp Vault token", Kind = SecretKind.ApiToken,
                Keywords = ["hvs.", "hvb.", "hvr."], Pattern = SecretPatterns.VaultToken(), Source = Gitleaks + "vault-service-token, vault-batch-token",
            },
            new()
            {
                Id = "terraform-api-token", DisplayName = "Terraform Cloud token", Kind = SecretKind.ApiToken,
                Keywords = [".atlasv1."], Pattern = SecretPatterns.TerraformApiToken(), Source = Gitleaks + "hashicorp-tf-api-token",
            },
            new()
            {
                Id = "onepassword-secret-key", DisplayName = "1Password Secret Key", Kind = SecretKind.GenericSecret,
                Keywords = ["A3-"], Pattern = SecretPatterns.OnePasswordSecretKey(), Source = Gitleaks + "1password-secret-key",
            },
            new()
            {
                Id = "onepassword-service-account-token", DisplayName = "1Password service account token", Kind = SecretKind.ApiToken,
                Keywords = ["ops_eyJ"], Pattern = SecretPatterns.OnePasswordServiceAccountToken(), Source = Gitleaks + "1password-service-account-token",
            },
            new()
            {
                Id = "bitwarden-api-client-id", DisplayName = "Bitwarden API client ID", Kind = SecretKind.AccessKeyId, IsIdentifier = true,
                Keywords = ["client_id", "client id", "clientid", "bitwarden"], Pattern = SecretPatterns.BitwardenApiClientId(),
                Source = "Bitwarden personal API key documentation (client_id user.<guid> / organization.<guid>)",
            },
            new()
            {
                Id = "kubeconfig-client-key", DisplayName = "Kubernetes client key", Kind = SecretKind.PrivateKey,
                Keywords = ["client-key-data"], Pattern = SecretPatterns.KubeconfigClientKey(),
                Target = PlacementTarget.Hidden("Kubernetes client key"),
                Source = "Kubernetes kubeconfig documentation (client-key-data)",
            },
            new()
            {
                Id = "kubeconfig-token", DisplayName = "Kubernetes token", Kind = SecretKind.ApiToken,
                Keywords = ["current-context"], Pattern = SecretPatterns.KubeconfigToken(),
                Source = "Kubernetes kubeconfig documentation (users[].user.token)",
            },
            new()
            {
                Id = "age-secret-key", DisplayName = "age identity", Kind = SecretKind.GenericSecret, Confidence = Confidence.High,
                Validator = Bech32.IsValidAgeSecretKey, Keywords = ["AGE-SECRET-KEY-1"], Pattern = SecretPatterns.AgeSecretKey(),
                Source = Gitleaks + "age-secret-key; bech32 checksum per BIP173",
            },
            new()
            {
                Id = "wireguard-key", DisplayName = "WireGuard key", Kind = SecretKind.GenericSecret,
                Keywords = ["PrivateKey", "PresharedKey"], Pattern = SecretPatterns.WireGuardKey(),
                Source = "wg(8) configuration format (32-byte base64 keys)",
            },

            // Credentials in URLs and connection strings.
            new()
            {
                Id = "database-uri", DisplayName = "Database URI", Kind = SecretKind.ConnectionString,
                Keywords = ["postgres", "mysql", "mariadb", "mongodb", "redis", "amqp", "mssql", "sqlserver"],
                Pattern = SecretPatterns.DatabaseUri(), Validator = HasRealUrlPassword,
                Target = PlacementTarget.LoginFromConnection,
                Source = Betterleaks + "generic-credential-uri, mongodb-connection-string",
            },
            new()
            {
                Id = "url-credentials", DisplayName = "URL credentials", Kind = SecretKind.UrlCredentials,
                Keywords = ["://"], Pattern = SecretPatterns.UrlCredentials(), Validator = HasRealUrlPassword,
                Target = PlacementTarget.LoginFromConnection,
                Source = Betterleaks + "generic-credential-uri",
            },
            new()
            {
                Id = "connection-string", DisplayName = "Connection string", Kind = SecretKind.ConnectionString,
                Keywords = ["password=", "pwd="], Pattern = SecretPatterns.ConnectionString(), Validator = IsConnectionString,
                Target = PlacementTarget.LoginFromConnection,
                Source = "ADO.NET and libpq connection string keywords (vendor documentation)",
            },

            // Other formats.
            new()
            {
                Id = "jwt", DisplayName = "JWT", Kind = SecretKind.Jwt, Validator = JwtValidator.HasAlgorithmHeader,
                Keywords = ["eyJ"], Pattern = SecretPatterns.Jwt(), Target = PlacementTarget.Hidden("JWT"),
                Source = Gitleaks + "jwt; header check per RFC 7519",
            },
            new()
            {
                Id = "otpauth-totp", DisplayName = "TOTP", Kind = SecretKind.Totp, Confidence = Confidence.High,
                Validator = static value => TotpUri.TryNormalize(value, null, null, out _),
                Keywords = ["otpauth://"], Pattern = SecretPatterns.OtpauthTotp(),
                Source = "Google Authenticator Key Uri Format",
            },
            new()
            {
                Id = "otpauth-hotp", DisplayName = "HOTP", Kind = SecretKind.Totp, Confidence = Confidence.Medium,
                Validator = static value => value.Contains("secret=", StringComparison.OrdinalIgnoreCase),
                Keywords = ["otpauth://"], Pattern = SecretPatterns.OtpauthHotp(),
                Target = PlacementTarget.NotMoved("HOTP is not supported by Bitwarden"),
                Source = "Google Authenticator Key Uri Format",
            },
            new()
            {
                Id = "otpauth-migration", DisplayName = "TOTP", Kind = SecretKind.Totp, Confidence = Confidence.High,
                Validator = static value => TotpUri.DecodeMigration(value).Count > 0,
                Keywords = ["otpauth-migration://"], Pattern = SecretPatterns.OtpauthMigration(),
                Source = "dim13/otpauth migration.proto @027a7cc0957463b4a5eaefb26837e8e26cfd5f67",
            },
            new()
            {
                Id = "steam-totp", DisplayName = "TOTP", Kind = SecretKind.Totp, Confidence = Confidence.High,
                Validator = static value => TotpUri.TryNormalize(value, null, null, out _),
                Keywords = ["steam://"], Pattern = SecretPatterns.SteamTotp(),
                Source = "Bitwarden TOTP field documentation (steam:// prefix)",
            },
        ];
    }

    private static int? EndAfter(string text, int start, string marker, string suffix)
    {
        int limit = Math.Min(text.Length, start + MaxStructureLength);
        int end = text.IndexOf(marker, start, limit - start, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        int lineEnd = text.IndexOfAny(['\r', '\n'], end);
        lineEnd = lineEnd < 0 ? text.Length : lineEnd;
        return suffix.Length == 0 || text.AsSpan(end, lineEnd - end).EndsWith(suffix, StringComparison.Ordinal) ? lineEnd : null;
    }

    private static int? EndOfLineAfter(string text, int start, string marker)
    {
        return EndAfter(text, start, marker, string.Empty);
    }

    private static bool IsSasUrl(string value)
    {
        int question = value.IndexOf('?', StringComparison.Ordinal);
        if (question < 0)
        {
            return false;
        }

        string[] keys = [.. value[(question + 1)..].Split('&').Select(pair => pair.Split('=')[0])];
        return keys.Contains("sig", StringComparer.OrdinalIgnoreCase) && keys.Contains("sv", StringComparer.OrdinalIgnoreCase) && keys.Contains("se", StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasRealUrlPassword(string value)
    {
        return ConnectionParts.TryParseUri(value, out ConnectionParts? parts) && parts is { Password: { } password }
            && !CandidateFilter.IsPlaceholder(password) && !CandidateFilter.IsRepeatedOrSequential(password);
    }

    private static bool IsConnectionString(string value)
    {
        return ConnectionParts.TryParseConnectionString(value, out ConnectionParts? parts) && parts is { Password: { } password, Host: not null }
            && !CandidateFilter.IsPlaceholder(password);
    }
}
