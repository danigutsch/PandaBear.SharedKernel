using System.Text;

namespace SharedKernel.RepoConfig.Tests;

internal static class RoadmapGitStatusTestData
{
    private const string Hash = "0000000000000000000000000000000000000000";

    public static byte[] Ordinary(
        string path,
        string headMode = "100644",
        string indexMode = "100644",
        string workTreeMode = "100644",
        string submodule = "N...") =>
        Encoding.UTF8.GetBytes($"1 .M {submodule} {headMode} {indexMode} {workTreeMode} {Hash} {Hash} {path}\0");

    public static byte[] Rename(string path, string originalPath) =>
        Encoding.UTF8.GetBytes($"2 R. N... 100644 100644 100644 {Hash} {Hash} R100 {path}\0{originalPath}\0");

    public static byte[] Copy(string path, string originalPath) =>
        Encoding.UTF8.GetBytes($"2 C. N... 100644 100644 100644 {Hash} {Hash} C100 {path}\0{originalPath}\0");

    public static byte[] Untracked(string path) => Encoding.UTF8.GetBytes($"? {path}\0");
}
