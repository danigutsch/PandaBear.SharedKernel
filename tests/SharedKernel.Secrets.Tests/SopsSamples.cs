using System.Text;

namespace PandaBear.Secrets.Tests;

/// <summary>Builds encrypted-looking SOPS documents in the layout that sops writes, with placeholder ciphertext.</summary>
internal static class SopsSamples
{
    /// <summary>A SOPS YAML document encrypted to <paramref name="recipients"/>, with the data keys before the metadata as sops writes them.</summary>
    public static string Yaml(string[] recipients, string extraSopsLines = "")
    {
        StringBuilder text = new();
        text.Append("db:\n    password: ENC[AES256_GCM,data:abc,iv:def,tag:ghi,type:str]\nlist:\n    - ENC[AES256_GCM,data:x,type:str]\nsops:\n    kms: []\n    gcp_kms: []\n    azure_kv: []\n    hc_vault: []\n");
        text.Append("    age:\n");
        foreach (var recipient in recipients)
        {
            text.Append("        - recipient: ").Append(recipient).Append('\n');
            text.Append("          enc: |\n            -----BEGIN AGE ENCRYPTED FILE-----\n            YWdlLWVuY3J5cHRpb24ub3JnL3YxCi0+\n            -----END AGE ENCRYPTED FILE-----\n");
        }

        text.Append(extraSopsLines);
        text.Append("    lastmodified: \"2026-11-09T17:36:17Z\"\n    mac: ENC[AES256_GCM,data:zzz,iv:yyy,tag:xxx,type:str]\n    pgp: []\n    unencrypted_suffix: _unencrypted\n    version: 3.13.0\n");
        return text.ToString();
    }

    /// <summary>A SOPS JSON document encrypted to <paramref name="recipients"/>, with extra raw members inside the sops object.</summary>
    public static string Json(string[] recipients, string extraMembers = "")
    {
        var age = string.Join(",", recipients.Select(static r => $"{{\"recipient\":\"{r}\",\"enc\":\"-----BEGIN AGE ENCRYPTED FILE-----\\nAAAA\\n-----END AGE ENCRYPTED FILE-----\\n\"}}"));
        return $"{{\"db\":{{\"password\":\"ENC[x]\"}},\"sops\":{{\"kms\":null,\"gcp_kms\":null,\"pgp\":null,\"age\":[{age}],\"lastmodified\":\"2026-11-09T17:36:17Z\",\"mac\":\"ENC[y]\",\"version\":\"3.13.0\"{extraMembers}}}}}";
    }
}
