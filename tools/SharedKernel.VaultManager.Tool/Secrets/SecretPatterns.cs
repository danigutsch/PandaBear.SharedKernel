using System.Text.RegularExpressions;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>
/// Source-generated patterns for secret detection. Every pattern uses explicit ASCII classes, bounded quantifiers,
/// <see cref="RegexOptions.CultureInvariant"/>, and a timeout of <see cref="TimeoutMilliseconds"/>.
/// </summary>
internal static partial class SecretPatterns
{
    /// <summary>
    /// Match timeout of every pattern, in milliseconds. It stops catastrophic backtracking, which the bounded quantifiers
    /// of the patterns are meant to rule out. A match on real input normally takes far less, but a pause of the whole
    /// process, such as a garbage collection on a machine that is short of processor time, can exceed any timeout, so
    /// the scanner runs a timed-out match once more (<see cref="PatternRetry"/>) before it flags the text for review.
    /// </summary>
    public const int TimeoutMilliseconds = 1000;

    /// <summary>Pattern <c>GitHubToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_])gh[pousr]_[A-Za-z0-9]{36}(?![A-Za-z0-9_])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex GitHubToken();

    /// <summary>Pattern <c>GitHubFineGrainedPat</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_])github_pat_[A-Za-z0-9_]{82}(?![A-Za-z0-9_])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex GitHubFineGrainedPat();

    /// <summary>Pattern <c>GitLabPat</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])glpat-[A-Za-z0-9_-]{20,300}(?:\.[A-Za-z0-9_-]{1,20}){0,3}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex GitLabPat();

    /// <summary>Pattern <c>GitLabToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])(?:gldt|glrtr|glrt|glcbt|glptt|glft|glimt|glagent|gloas|glsoat|glffct|glwt)-[A-Za-z0-9_-]{20,300}(?:\.[A-Za-z0-9_-]{1,20}){0,3}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex GitLabToken();

    /// <summary>Pattern <c>NpmToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_])npm_[A-Za-z0-9]{36}(?![A-Za-z0-9_])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex NpmToken();

    /// <summary>Pattern <c>PypiToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])pypi-AgEIcHlwaS5vcmc[A-Za-z0-9_-]{50,1000}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex PypiToken();

    /// <summary>Pattern <c>DockerHubToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])dckr_(?:pat_[A-Za-z0-9_-]{27}|oat_[A-Za-z0-9_-]{32})(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex DockerHubToken();

    /// <summary>Pattern <c>NuGetApiKey</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_])oy2[a-p][a-z2-7]{15}[aq][a-z2-7]{25}[aeimquy4](?![A-Za-z0-9_])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex NuGetApiKey();

    /// <summary>Pattern <c>AwsAccessKeyId</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_])(?:AKIA|ASIA|ABIA|ACCA|A3T[A-Z0-9])[A-Z2-7]{16}(?![A-Za-z0-9_])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex AwsAccessKeyId();

    /// <summary>Pattern <c>AwsSecretAccessKey</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?i:aws_?secret_?access_?key|secret[ _]?access[ _]?key)[ \t""']{0,3}[:=][ \t""']{0,5}(?<secret>[A-Za-z0-9/+]{40})(?![A-Za-z0-9/+=])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex AwsSecretAccessKey();

    /// <summary>Pattern <c>AwsSecretCandidate</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9/+=])[A-Za-z0-9/+]{40}(?![A-Za-z0-9/+=])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex AwsSecretCandidate();

    /// <summary>Pattern <c>GcpApiKey</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])AIza[A-Za-z0-9_-]{35}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex GcpApiKey();

    /// <summary>Pattern <c>GoogleOAuthClientSecret</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])GOCSPX-[A-Za-z0-9_-]{28,32}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex GoogleOAuthClientSecret();

    /// <summary>Pattern <c>GoogleOAuthClientId</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])[0-9]{1,20}-[a-z0-9]{32}\.apps\.googleusercontent\.com(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex GoogleOAuthClientId();

    /// <summary>Pattern <c>JsonObject</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\{[^{}]{1,3500}\}", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex JsonObject();

    /// <summary>Pattern <c>AzureStorageAccountKey</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"AccountKey=(?<secret>[A-Za-z0-9+/]{86}==)", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex AzureStorageAccountKey();

    /// <summary>Pattern <c>EntraClientSecret</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_~.-])[A-Za-z0-9_~.]{3}[78]Q~[A-Za-z0-9_~.-]{31,34}(?![A-Za-z0-9_~.-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex EntraClientSecret();

    /// <summary>Pattern <c>AzureSasUrl</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"https://[A-Za-z0-9.-]{1,253}(?:/[^ \t\r\n?""'<>]{0,1024})?\?[^ \t\r\n""'<>]{1,2048}", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex AzureSasUrl();

    /// <summary>Pattern <c>DigitalOceanToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_])do[por]_v1_[a-f0-9]{64}(?![A-Za-z0-9_])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex DigitalOceanToken();

    /// <summary>Pattern <c>CloudflareApiToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_])cf[ua]t_[A-Za-z0-9]{40}[a-f0-9]{8}(?![A-Za-z0-9_])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex CloudflareApiToken();

    /// <summary>Pattern <c>SlackBotToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])xoxb-[0-9]{8,14}-[A-Za-z0-9-]{10,128}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex SlackBotToken();

    /// <summary>Pattern <c>SlackUserToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])xox[pe](?:-[0-9]{10,13}){3}-[A-Za-z0-9-]{28,34}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex SlackUserToken();

    /// <summary>Pattern <c>SlackAppToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])xapp-[0-9]-[A-Za-z0-9]{1,32}-[0-9]{1,32}-[A-Za-z0-9]{1,128}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex SlackAppToken();

    /// <summary>Pattern <c>SlackWebhookUrl</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?:https?://)?hooks\.slack\.com/(?:services|workflows|triggers)/[A-Za-z0-9+/]{43,56}(?![A-Za-z0-9+/])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex SlackWebhookUrl();

    /// <summary>Pattern <c>StripeSecretKey</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_])(?:sk|rk)_(?:live|test|prod)_[A-Za-z0-9]{10,247}(?![A-Za-z0-9_])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex StripeSecretKey();

    /// <summary>Pattern <c>StripeWebhookSecret</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_])whsec_[A-Za-z0-9+/=]{24,128}(?![A-Za-z0-9_+/=])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex StripeWebhookSecret();

    /// <summary>Pattern <c>StripePublishableKey</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_])pk_(?:live|test)_[A-Za-z0-9]{10,247}(?![A-Za-z0-9_])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex StripePublishableKey();

    /// <summary>Pattern <c>OpenAiApiKey</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])(?:sk-(?:proj|svcacct|admin)-(?:[A-Za-z0-9_-]{74}|[A-Za-z0-9_-]{58})T3BlbkFJ(?:[A-Za-z0-9_-]{74}|[A-Za-z0-9_-]{58})|sk-[A-Za-z0-9]{20}T3BlbkFJ[A-Za-z0-9]{20})(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex OpenAiApiKey();

    /// <summary>Pattern <c>AnthropicApiKey</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])sk-ant-(?:api03|admin01)-[A-Za-z0-9_-]{93}AA(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex AnthropicApiKey();

    /// <summary>Pattern <c>TwilioSid</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_])(?:AC|SK)[0-9a-fA-F]{32}(?![A-Za-z0-9_])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex TwilioSid();

    /// <summary>Pattern <c>TwilioAuthToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?i:auth[ _-]?token)[ \t""']{0,3}[:=][ \t""']{0,5}(?<secret>[0-9a-f]{32})(?![A-Za-z0-9_])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex TwilioAuthToken();

    /// <summary>Pattern <c>SendGridApiKey</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])SG\.[A-Za-z0-9_-]{22}\.[A-Za-z0-9_-]{43}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex SendGridApiKey();

    /// <summary>Pattern <c>DiscordBotToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])[MNO][A-Za-z0-9_-]{23,25}\.[A-Za-z0-9_-]{6}\.[A-Za-z0-9_-]{27,38}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex DiscordBotToken();

    /// <summary>Pattern <c>TelegramBotToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_])[0-9]{8,10}:A[A-Za-z0-9_-]{34}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex TelegramBotToken();

    /// <summary>Pattern <c>ShopifyToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_])shp(?:at|ca|pa|ss)_[a-fA-F0-9]{32}(?![A-Za-z0-9_])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex ShopifyToken();

    /// <summary>Pattern <c>TailscaleKey</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])tskey-(?:auth|api|client|scim|webhook)-[A-Za-z0-9]{1,64}-[A-Za-z0-9]{1,64}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex TailscaleKey();

    /// <summary>Pattern <c>VaultToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_])hv[sbr]\.[A-Za-z0-9_-]{24,300}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex VaultToken();

    /// <summary>Pattern <c>TerraformApiToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_])[A-Za-z0-9]{14}\.atlasv1\.[A-Za-z0-9_=-]{60,70}(?![A-Za-z0-9_=-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex TerraformApiToken();

    /// <summary>Pattern <c>OnePasswordSecretKey</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])A3-[A-Z0-9]{6}-(?:[A-Z0-9]{11}|[A-Z0-9]{6}-[A-Z0-9]{5})-[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex OnePasswordSecretKey();

    /// <summary>Pattern <c>OnePasswordServiceAccountToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_])ops_eyJ[A-Za-z0-9+/]{250,3000}={0,3}(?![A-Za-z0-9+/=])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex OnePasswordServiceAccountToken();

    /// <summary>Pattern <c>BitwardenApiClientId</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_.-])(?:user|organization)\.[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex BitwardenApiClientId();

    /// <summary>Pattern <c>KubeconfigClientKey</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"client-key-data:[ \t]{0,8}(?<secret>[A-Za-z0-9+/=]{20,3000})", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex KubeconfigClientKey();

    /// <summary>Pattern <c>KubeconfigToken</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<=(?:\A|\n)[ \t]{0,32})token:[ \t]{0,8}(?<secret>[A-Za-z0-9._~+/=-]{20,2048})[ \t]{0,8}(?=\r?\n|\z)", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex KubeconfigToken();

    /// <summary>Pattern <c>PemPrivateKeyHeader</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"-----BEGIN (?<label>(?:RSA |EC |DSA |ENCRYPTED |OPENSSH )?PRIVATE KEY)-----", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex PemPrivateKeyHeader();

    /// <summary>Pattern <c>Ssh2EncryptedHeader</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"---- BEGIN SSH2 ENCRYPTED PRIVATE KEY ----", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex Ssh2EncryptedHeader();

    /// <summary>Pattern <c>PgpPrivateKeyHeader</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"-----BEGIN PGP PRIVATE KEY BLOCK-----", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex PgpPrivateKeyHeader();

    /// <summary>Pattern <c>PuttyHeader</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"PuTTY-User-Key-File-[23]: [A-Za-z0-9@.-]{1,64}", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex PuttyHeader();

    /// <summary>Pattern <c>AgeSecretKey</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])AGE-SECRET-KEY-1[QPZRY9X8GF2TVDW0S3JN54KHCE6MUA7L]{58}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex AgeSecretKey();

    /// <summary>Pattern <c>WireGuardKey</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?:PrivateKey|PresharedKey)[ \t]{0,8}=[ \t]{0,8}(?<secret>[A-Za-z0-9+/]{42}[AEIMQUYcgkosw480]=)(?![A-Za-z0-9+/=])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex WireGuardKey();

    /// <summary>Pattern <c>DatabaseUri</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9+.-])(?i:postgres(?:ql)?|mysql|mariadb|mongodb(?:\+srv)?|rediss?|amqps?|mssql|sqlserver)://[^:/@ \t\r\n""'<>]{0,128}:[^/@ \t\r\n""'<>]{1,256}@(?:\[[0-9A-Fa-f:.]{2,45}\]|[A-Za-z0-9][A-Za-z0-9._-]{0,252})(?::[0-9]{1,5})?(?:,[A-Za-z0-9][A-Za-z0-9._-]{0,252}(?::[0-9]{1,5})?){0,10}(?:[/?][A-Za-z0-9._~!$&*+,;=:@%/?-]{0,1024})?", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex DatabaseUri();

    /// <summary>Pattern <c>UrlCredentials</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9+.-])[A-Za-z][A-Za-z0-9+.-]{1,20}://[^:/@ \t\r\n""'<>]{1,128}:[^/@ \t\r\n""'<>]{1,256}@(?:\[[0-9A-Fa-f:.]{2,45}\]|[A-Za-z0-9][A-Za-z0-9._-]{0,252})(?::[0-9]{1,5})?(?:[/?#][A-Za-z0-9._~!$&*+,;=:@%/?#-]{0,1024})?", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex UrlCredentials();

    /// <summary>Pattern <c>ConnectionString</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![^ \t\r\n""'])(?:[A-Za-z][A-Za-z0-9 _]{0,40}=[^;\r\n]{0,512};[ \t]{0,4}){1,30}(?:[A-Za-z][A-Za-z0-9 _]{0,40}=[^;\r\n]{1,512})?", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex ConnectionString();

    /// <summary>Pattern <c>Jwt</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])eyJ[A-Za-z0-9_-]{10,2048}\.eyJ[A-Za-z0-9_-]{10,2048}\.[A-Za-z0-9_-]{0,1024}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex Jwt();

    /// <summary>Pattern <c>OtpauthTotp</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"otpauth://(?i:totp)/[^ \t\r\n""'<>]{1,2048}", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex OtpauthTotp();

    /// <summary>Pattern <c>OtpauthHotp</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"otpauth://(?i:hotp)/[^ \t\r\n""'<>]{1,2048}", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex OtpauthHotp();

    /// <summary>Pattern <c>OtpauthMigration</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"otpauth-migration://offline\?data=[A-Za-z0-9%+/=_-]{4,3000}", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex OtpauthMigration();

    /// <summary>Pattern <c>SteamTotp</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"steam://[A-Za-z2-7]{16,128}(?![A-Za-z0-9])", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex SteamTotp();

    /// <summary>Pattern <c>MaFileSharedSecret</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"""shared_secret""[ \t]{0,8}:[ \t]{0,8}""(?<secret>[A-Za-z0-9+/=]{20,64})""", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex MaFileSharedSecret();

    /// <summary>Pattern <c>MaFileRevocationCode</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"""revocation_code""[ \t]{0,8}:[ \t]{0,8}""(?<secret>R[0-9]{5})""", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex MaFileRevocationCode();

    /// <summary>Pattern <c>NotesLine</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A[ \t]*(?<key>[^\r\n:=]{1,40}?)[ \t]*[:=][ \t]*(?<value>[^\r\n]+?)[ \t]*\z", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex NotesLine();

    /// <summary>Pattern <c>Uuid</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\z", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex Uuid();

    /// <summary>Pattern <c>DateOrTime</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A(?:[0-9]{4}[-/.][0-9]{1,2}[-/.][0-9]{1,2}(?:[T ][0-9]{1,2}:[0-9]{2}(?::[0-9]{2}(?:\.[0-9]{1,9})?)?(?:Z|[+-][0-9]{2}:?[0-9]{2})?)?|[0-9]{1,2}[-/.][0-9]{1,2}[-/.][0-9]{2,4}|[0-9]{1,2}:[0-9]{2}(?::[0-9]{2})?)\z", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex DateOrTime();

    /// <summary>Pattern <c>SemVer</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A[vV]?[0-9]{1,9}\.[0-9]{1,9}\.[0-9]{1,9}(?:[-+][0-9A-Za-z.+-]{1,64})?\z", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex SemVer();

    /// <summary>Pattern <c>Email</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A[A-Za-z0-9._%+-]{1,64}@[A-Za-z0-9.-]{1,253}\.[A-Za-z]{2,24}\z", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex Email();

    /// <summary>Pattern <c>PlainUrl</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A[A-Za-z][A-Za-z0-9+.-]{1,20}://[^ \t\r\n@]{1,2048}\z", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex PlainUrl();

    /// <summary>Pattern <c>FilePath</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A(?:[A-Za-z]:\\|\\\\|~/|\./|\.\./|/)[^ \t\r\n]{0,1024}\z", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex FilePath();

    /// <summary>Pattern <c>TemplatePlaceholder</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A(?:\$\{[^}]{0,128}\}|\{\{[^}]{0,128}\}\}|%[A-Za-z0-9_]{1,64}%|<[^<>]{1,64}>|\$[A-Z_][A-Z0-9_]{0,63})\z", RegexOptions.CultureInvariant, TimeoutMilliseconds)]
    public static partial Regex TemplatePlaceholder();
}
