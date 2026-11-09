namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Public recipients of throwaway keys and sample SOPS documents, for tests.</summary>
internal static class TestRecipients
{
    /// <summary>A valid X25519 recipient.</summary>
    public const string First = "age1s6ljywnmxkxndew9qztcjgfa3lsqfwe0rec66a9jjugzuxaagg3sgumkyf";

    /// <summary>A second valid X25519 recipient.</summary>
    public const string Second = "age1pz4srxz4yn6h4puyx9mgzk63pn4tuhyla67rrglluyqjnlhyz5sqx2vc4h";

    /// <summary>A third valid X25519 recipient.</summary>
    public const string Third = "age1dquc283cn3pehdzu8pyk68ucd4eq5x6d9ru5qj67zdd7pq7gkdeq5raw4x";

    /// <summary>A well-formed encrypted value as sops writes it.</summary>
    public const string Encrypted = "ENC[AES256_GCM,data:Zhnqwg==,iv:AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=,tag:AAECAwQFBgcICQoLDA0ODw==,type:str]";

    /// <summary>A SOPS JSON document encrypted to <paramref name="recipients"/>, with extra raw members inside the sops object.</summary>
    public static string Json(string[] recipients, string extraMembers = "")
    {
        var age = string.Join(",", recipients.Select(static r => $"{{\"recipient\":\"{r}\",\"enc\":{AgeEnvelopes.JsonLiteral(r)}}}"));
        return $"{{\"db\":{{\"password\":\"{Encrypted}\"}},\"sops\":{{\"kms\":null,\"age\":[{age}],\"version\":\"3.13.0\"{extraMembers}}}}}";
    }

    /// <summary>A SOPS YAML document encrypted to <paramref name="recipients"/>, with extra raw lines inside the sops block and before it.</summary>
    public static string Yaml(string[] recipients, string extraSopsLines = "", string dataLines = "") =>
        $"db:\n    password: {Encrypted}\n{dataLines}{Block(recipients, extraSopsLines)}";

    /// <summary>A bare <c>sops:</c> block that lists <paramref name="recipients"/>.</summary>
    public static string Block(string[] recipients, string extraSopsLines = "") =>
        "sops:\n    kms: []\n    age:\n"
        + string.Concat(recipients.Select(static r => $"        - recipient: {r}\n          enc: |\n{AgeEnvelopes.Indented(r, 12)}"))
        + extraSopsLines
        + "    unencrypted_suffix: _unencrypted\n    version: 3.13.0\n";

    /// <summary>A configuration with one rule for <c>secrets/</c> that requires <paramref name="recipients"/>.</summary>
    public static string Config(int minimum, params string[] recipients) =>
        $"{{\"version\":1,\"creationRules\":[{{\"pathRegex\":\"^secrets/\",\"recipients\":[{string.Join(",", recipients.Select(static r => $"\"{r}\""))}],\"minimumRecipients\":{minimum}}}]}}";
}
