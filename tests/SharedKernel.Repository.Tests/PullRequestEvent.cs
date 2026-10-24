using System.Text.Json;

namespace PandaBear.Repository.Tests;

/// <summary>Writes GitHub pull_request event files.</summary>
internal static class PullRequestEvent
{
    public static Task Write(string path, string title, string body, CancellationToken ct) =>
        File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            pull_request = new
            {
                title,
                body
            }
        }), ct);
}
