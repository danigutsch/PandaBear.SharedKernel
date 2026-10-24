using System.Text.Json;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// The values of a repository JSON file that a license review approved.
/// </summary>
/// <param name="File">Repository-relative path of the JSON file.</param>
/// <param name="Values">The approved string or boolean value of each top-level property of the file.</param>
internal sealed record LicenseApprovalConfiguration(string File, OrderedDictionary<string, JsonElement> Values);
