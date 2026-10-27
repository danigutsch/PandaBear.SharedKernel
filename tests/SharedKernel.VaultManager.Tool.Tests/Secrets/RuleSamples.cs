using System.Security.Cryptography;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>Positive and negative text for every catalog rule, assembled at runtime.</summary>
internal static class RuleSamples
{
    private const string Digits = "0123456789";
    private const string LowerAlphanumeric = "abcdefghijklmnopqrstuvwxyz0123456789";
    private const string UpperAlphanumeric = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>Pairs every catalog rule id with a text that matches it and a text that does not.</summary>
    public static IReadOnlyList<(string RuleId, string Positive, string Negative)> All { get; } = Build();

    /// <summary>Returns the rule ids of <see cref="All"/> as theory data.</summary>
    public static TheoryData<string> Ids()
    {
        var data = new TheoryData<string>();
        foreach (var sample in All)
        {
            data.Add(sample.RuleId);
        }

        return data;
    }

    private static string R(string alphabet, int length, int seed) => TestSecrets.FromAlphabet(alphabet, length, seed);

    private static List<(string, string, string)> Build()
    {
        string a = TestSecrets.Alphanumeric;
        string hex = TestSecrets.LowerHex;
        string b64 = TestSecrets.Base64;
        string url = TestSecrets.Base64Url;
        string nugetBody = "oy2" + R("abcdefghijklmnop", 1, 1) + R("abcdefghijklmnopqrstuvwxyz234567", 15, 2) + "q"
            + R("abcdefghijklmnopqrstuvwxyz234567", 25, 3) + "e";
        string githubToken = TestSecrets.GitHubToken("ghs", 4);
        string pemHeader = PemFixtures.Boundary("BEGIN", "PRIVATE KEY");
        string pemFooter = PemFixtures.Boundary("END", "PRIVATE KEY");
        string serviceAccount = "{\n  \"type\": \"service_account\",\n  \"project_id\": \"demo\",\n  \"private_key\": \"" + pemHeader + "\\n"
            + R(b64, 120, 5) + "\\n" + pemFooter + "\\n\",\n  \"client_email\": \"svc@demo.iam.gserviceaccount.com\"\n}";
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] ageKey = TestSecrets.Bytes(6, 32);
        string age = Bech32Reference.Encode("age-secret-key-", ageKey).ToUpperInvariant();
        string ageBroken = age[..^1] + (age[^1] == 'Q' ? 'P' : 'Q');
        string migration = MigrationPayloadBuilder.Uri((TestSecrets.Bytes(7, 20), "user", "Issuer", 1, 1, 2));
        string putty = "PuTTY-User-Key-File-3: ssh-ed25519\nEncryption: none\nComment: fixture\nPublic-Lines: 2\n" + R(b64, 64, 8)
            + "\n" + R(b64, 4, 9) + "\nPrivate-Lines: 1\n" + R(b64, 44, 10) + "\nPrivate-MAC: " + R(hex, 64, 11);
        string pgpBody = "\n\n" + R(b64, 64, 12) + "\n" + R(b64, 64, 13) + "\n=" + R(b64, 4, 14) + "\n";
        string ssh2Body = "\nComment: \"fixture\"\n" + R(b64, 64, 15) + "\n" + R(b64, 64, 16) + "\n";

        // Assemble the synthetic format prefix at runtime so source scans do not mistake it for a credential.
        string pypiPrefix = string.Concat("pypi-", "AgEI", "cHlwaS5vcmc");

        return
        [
            ("github-token", "token " + githubToken, "token ghx_" + R(a, 36, 17)),
            ("github-fine-grained-pat", "github_pat_" + R(a + "_", 82, 18), "github_pat_" + R(a, 60, 19)),
            ("gitlab-pat", "glpat-" + R(url, 20, 20), "glpat-" + R(a, 10, 21)),
            ("gitlab-token", "runner glrt-" + R(url, 20, 22), "runner glxx-" + R(a, 20, 23)),
            ("npm-token", TestSecrets.NpmToken(24), "npm_" + R(a, 35, 25)),
            ("pypi-token", pypiPrefix + R(url, 60, 26), pypiPrefix + R(a, 10, 27)),
            ("dockerhub-token", "dckr_pat_" + R(url, 27, 28), "dckr_pat_" + R(a, 20, 29)),
            ("nuget-api-key", "key " + nugetBody, "key oy2" + R("ABCDEFGHIJKLMNOPQRSTUVWXYZ", 43, 30)),
            ("aws-access-key-id", "id AKIA" + R("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567", 16, 31), "id AKIA" + R("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567", 15, 32)),
            ("aws-secret-access-key", "aws_secret_access_key = " + R(b64, 40, 33), "aws_secret_access_key = " + R(b64, 39, 34)),
            ("gcp-api-key", "AIza" + R(url, 35, 35), "AIza" + R(a, 34, 36)),
            ("google-oauth-client-secret", "GOCSPX-" + R(url, 28, 37), "GOCSPX-" + R(a, 10, 38)),
            ("google-oauth-client-id", "123456789012-" + R(LowerAlphanumeric, 32, 39) + ".apps.googleusercontent.com", "123456789012-" + R(LowerAlphanumeric, 31, 40) + ".apps.googleusercontent.com"),
            ("gcp-service-account", serviceAccount, serviceAccount.Replace("service_account", "authorized_user", StringComparison.Ordinal)),
            ("azure-storage-account-key", "DefaultEndpointsProtocol=https;AccountName=demo;AccountKey=" + R(b64, 86, 41) + "==", "AccountKey=" + R(b64, 80, 42) + "=="),
            ("entra-client-secret", "secret abc8Q~" + R(a + "_~.-", 34, 43), "secret abc5Q~" + R(a, 34, 44)),
            ("azure-sas-url", "https://demo.blob.core.windows.net/c/f.txt?sv=2022-11-02&sp=r&se=2030-01-01T00:00:00Z&sig=" + R(a, 43, 45) + "%3D", "https://demo.blob.core.windows.net/c/f.txt?sp=r&sig=" + R(a, 43, 46)),
            ("digitalocean-token", "dop_v1_" + R(hex, 64, 47), "dop_v1_" + R(hex, 63, 48)),
            ("cloudflare-api-token", "cfut_" + R(a, 40, 49) + R(hex, 8, 50), "cfxt_" + R(a, 40, 51) + R(hex, 8, 52)),
            ("slack-bot-token", "xoxb-" + R(Digits, 11, 53) + "-" + R(Digits, 12, 54) + "-" + R(a, 24, 55), "xoxb-12-ab"),
            ("slack-user-token", "xoxp-" + R(Digits, 10, 56) + "-" + R(Digits, 10, 57) + "-" + R(Digits, 12, 58) + "-" + R(a, 32, 59), "xoxp-123-" + R(a, 32, 60)),
            ("slack-app-token", "xapp-1-A" + R(UpperAlphanumeric, 10, 61) + "-" + R(Digits, 13, 62) + "-" + R(hex, 64, 63), "xapp-x-A" + R(UpperAlphanumeric, 10, 64)),
            ("slack-webhook-url", "https://hooks.slack.com/services/T" + R(UpperAlphanumeric, 8, 65) + "/B" + R(UpperAlphanumeric, 8, 66) + "/" + R(a, 24, 67), "https://hooks.slack.com/other/T" + R(UpperAlphanumeric, 8, 68)),
            ("stripe-secret-key", "sk_live_" + R(a, 24, 69), "sk_demo_" + R(a, 24, 70)),
            ("stripe-webhook-secret", "whsec_" + R(a, 32, 71), "whsec_" + R(a, 10, 72)),
            ("stripe-publishable-key", "pk_live_" + R(a, 24, 73), "pk_live_short"),
            ("openai-api-key", "sk-proj-" + R(url, 74, 74) + "T3BlbkFJ" + R(url, 74, 75), "sk-proj-" + R(a, 74, 76)),
            ("anthropic-api-key", "sk-ant-api03-" + R(url, 93, 77) + "AA", "sk-ant-api03-" + R(a, 93, 78) + "AB"),
            ("twilio-sid", "twilio account AC" + R(hex, 32, 79), "twilio account AC" + R(hex, 31, 80)),
            ("twilio-auth-token", "twilio auth_token: " + R(hex, 32, 81), "twilio auth_token: " + R(hex, 31, 82)),
            ("sendgrid-api-key", "SG." + R(url, 22, 83) + "." + R(url, 43, 84), "SG." + R(a, 21, 85) + "." + R(a, 43, 86)),
            ("discord-bot-token", "discord bot M" + R(url, 23, 87) + "." + R(url, 6, 88) + "." + R(url, 27, 89), "discord bot X" + R(a, 23, 90) + "." + R(a, 6, 91) + "." + R(a, 27, 92)),
            ("telegram-bot-token", "telegram bot 123456789:A" + R(url, 34, 93), "telegram bot 123456789:B" + R(a, 34, 94)),
            ("shopify-token", "shpat_" + R(hex, 32, 95), "shpat_" + R(hex, 31, 96)),
            ("tailscale-key", "tskey-auth-k" + R(a, 10, 97) + "-" + R(a, 30, 98), "tskey-foo-abc-def"),
            ("vault-token", "hvs." + R(url, 90, 99), "hvs.short"),
            ("terraform-api-token", R(LowerAlphanumeric, 14, 100) + ".atlasv1." + R(url, 64, 101), R(LowerAlphanumeric, 14, 102) + ".atlasv2." + R(a, 64, 103)),
            ("onepassword-secret-key", "A3-" + R(UpperAlphanumeric, 6, 104) + "-" + R(UpperAlphanumeric, 11, 105) + "-" + R(UpperAlphanumeric, 5, 106) + "-" + R(UpperAlphanumeric, 5, 107) + "-" + R(UpperAlphanumeric, 5, 108), "A3-ABC"),
            ("onepassword-service-account-token", "ops_eyJ" + R(b64, 300, 109), "ops_eyJ" + R(a, 100, 110)),
            ("bitwarden-api-client-id", "client_id: user." + Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e").ToString(), "client_id: user.notaguid"),
            ("kubeconfig-client-key", "client-key-data: " + R(b64, 400, 111), "client-certificate-data: " + R(b64, 400, 112)),
            ("kubeconfig-token", "apiVersion: v1\nkind: Config\ncurrent-context: dev\nusers:\n- name: dev\n  user:\n    token: " + R(url, 40, 113) + "\n", "apiVersion: v1\nkind: Config\ncurrent-context: dev\n"),
            ("pem-private-key", PemFixtures.Pkcs8(ecdsa), PemFixtures.Wrap("PUBLIC KEY", Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo()))),
            ("ssh2-encrypted-private-key", "---- BEGIN SSH2 ENCRYPTED PRIVATE KEY ----" + ssh2Body + "---- END SSH2 ENCRYPTED PRIVATE KEY ----", "---- BEGIN SSH2 PUBLIC KEY ----" + ssh2Body + "---- END SSH2 PUBLIC KEY ----"),
            ("pgp-private-key", PemFixtures.Boundary("BEGIN", "PGP PRIVATE KEY BLOCK") + pgpBody + PemFixtures.Boundary("END", "PGP PRIVATE KEY BLOCK"), PemFixtures.Boundary("BEGIN", "PGP PUBLIC KEY BLOCK") + pgpBody + PemFixtures.Boundary("END", "PGP PUBLIC KEY BLOCK")),
            ("putty-private-key", putty, putty.Replace("Encryption: none", "Encryption: blowfish", StringComparison.Ordinal)),
            ("age-secret-key", age, ageBroken),
            ("wireguard-key", "[Interface]\nPrivateKey = " + Convert.ToBase64String(TestSecrets.Bytes(114, 32)), "[Peer]\nPublicKey = " + Convert.ToBase64String(TestSecrets.Bytes(115, 32))),
            ("database-uri", "mysql://reporting:" + R(a, 16, 116) + "@db.example.net:3306/sales", "mysql://db.example.net:3306/sales"),
            ("url-credentials", "https://deploy:" + R(a, 16, 117) + "@git.example.com/repo.git", "https://deploy:changeme@git.example.com/repo.git"),
            ("connection-string", "Server=db.example;Database=app;User Id=app;Password=" + R(a, 16, 118) + ";", "Server=db.example;Database=app;Integrated Security=true;"),
            ("jwt", TestSecrets.Jwt(119), TestSecrets.Base64UrlEncode("{\"typ\":\"JWT\",\"kid\":\"x\"}") + "." + TestSecrets.Base64UrlEncode("{\"sub\":\"fixture-subject\"}") + "." + R(url, 43, 120)),
            ("otpauth-totp", "otpauth://totp/Demo?secret=" + TestSecrets.Base32Encode(TestSecrets.Bytes(121, 20)), "otpauth://totp/Demo?issuer=Demo"),
            ("otpauth-hotp", "otpauth://hotp/Demo?secret=" + TestSecrets.Base32Encode(TestSecrets.Bytes(122, 20)) + "&counter=1", "otpauth://hotp/Demo?counter=1"),
            ("otpauth-migration", migration, "otpauth-migration://offline?data=AAAA"),
            ("steam-totp", "steam://" + TestSecrets.Base32Encode(TestSecrets.Bytes(123, 10)), "steam://ABC"),
        ];
    }
}
