using System.Text;

namespace PandaBear.Secrets.Sops.Tests;

/// <summary>Builds SOPS documents in the layout that sops writes, with placeholder ciphertext and realistic age envelopes.</summary>
internal static class SopsSamples
{
    /// <summary>A well-formed encrypted value as sops writes it.</summary>
    public const string Encrypted = "ENC[AES256_GCM,data:Zhnqwg==,iv:AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=,tag:AAECAwQFBgcICQoLDA0ODw==,type:str]";

    /// <summary>A SOPS YAML document encrypted to <paramref name="recipients"/>, with the data keys before the metadata as sops writes them.</summary>
    public static string Yaml(string[] recipients, string extraSopsLines = "", string dataLines = "")
    {
        StringBuilder text = new();
        text.Append($"db:\n    password: {Encrypted}\nlist:\n    - {Encrypted}\n").Append(dataLines).Append("sops:\n    kms: []\n    gcp_kms: []\n    azure_kv: []\n    hc_vault: []\n");
        text.Append("    age:\n");
        foreach (var recipient in recipients)
        {
            text.Append("        - recipient: ").Append(recipient).Append('\n');
            text.Append("          enc: |\n");
            foreach (var line in AgeEnvelopes.For(recipient).TrimEnd('\n').Split('\n'))
            {
                text.Append("            ").Append(line).Append('\n');
            }
        }

        text.Append(extraSopsLines);
        text.Append("    lastmodified: \"2026-11-09T17:36:17Z\"\n    mac: ").Append(Encrypted).Append("\n    pgp: []\n    unencrypted_suffix: _unencrypted\n    version: 3.13.0\n");
        return text.ToString();
    }

    /// <summary>A SOPS JSON document encrypted to <paramref name="recipients"/>, with extra raw members inside the sops object and raw members in the data.</summary>
    public static string Json(string[] recipients, string extraMembers = "", string dataMembers = "")
    {
        var age = string.Join(",", recipients.Select(static r => $"{{\"recipient\":\"{r}\",\"enc\":{System.Text.Json.JsonSerializer.Serialize(AgeEnvelopes.For(r))}}}"));
        return $"{{\"db\":{{\"password\":\"{Encrypted}\"}}{dataMembers},\"sops\":{{\"kms\":null,\"gcp_kms\":null,\"pgp\":null,\"age\":[{age}],\"lastmodified\":\"2026-11-09T17:36:17Z\",\"mac\":\"{Encrypted}\",\"version\":\"3.13.0\"{extraMembers}}}}}";
    }

    /// <summary>A bare <c>sops:</c> block listing only <paramref name="recipients"/>, as a forger would write it.</summary>
    public static string FakeBlock(params string[] recipients) =>
        "sops:\n    age:\n" + string.Concat(recipients.Select(r => $"        - recipient: {r}\n          enc: |\n{AgeEnvelopes.Indented(r, 12)}")) + "    version: 3.13.0\n";
}
