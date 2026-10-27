using System.Text.Json;

namespace PandaBear.Repository.Tests;

/// <summary>Writes GitHub pull_request event files.</summary>
internal static class PullRequestEvent
{
    /// <summary>Writes a JSON event file at <paramref name="path"/> whose <c>pull_request</c> object holds the given <paramref name="title"/> and <paramref name="body"/>.</summary>
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
