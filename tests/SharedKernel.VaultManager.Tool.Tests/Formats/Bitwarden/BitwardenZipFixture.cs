namespace PandaBear.VaultManager.Tool.Tests.Formats.Bitwarden;

internal static class BitwardenZipFixture
{
    public const string DataJson = """
        {"encrypted":false,"folders":[],"items":[
          {"id":"i1","type":2,"name":"Report","secureNote":{"type":0}},
          {"id":"i2","type":2,"name":"Dup","secureNote":{"type":0}},
          {"id":"i3","type":2,"name":"Dup","secureNote":{"type":0}},
          {"id":"i4","type":2,"name":"a/b: c","secureNote":{"type":0}}
        ]}
        """;

    public static string Write(string directory)
    {
        string path = Path.Combine(directory, "export.zip");
        File.WriteAllBytes(path, TestZip.Create(
            ("data.json", TestZip.Utf8(DataJson)),
            ("attachments/Report/scan.pdf", "%PDF"u8.ToArray()),
            ("attachments/Dup/one.txt", [0x61]),
            ("attachments/Dup_1/two.txt", [0x62]),
            ("attachments/a_b_ c/key.txt", [0x61, 0x62, 0x63]),
            ("attachments/Orphan/lost.txt", [0x63])));
        return path;
    }
}
