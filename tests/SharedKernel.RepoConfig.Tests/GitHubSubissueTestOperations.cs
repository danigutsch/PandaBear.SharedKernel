using System.Globalization;
using System.Net;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Test helpers for the GitHub sub-issue command tests.</summary>
internal static class GitHubSubissueTestOperations
{
    private const string Repository = "owner/repository";
    private const string ApiRoot = "https://api.github.com/repos/owner/repository/issues/";

    /// <summary>Creates a handler that answers with parent issue 10 and child issue 20.</summary>
    public static TestHttpMessageHandler Issues()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, Issue(10, 111));
        handler.EnqueueJson(HttpStatusCode.OK, Issue(20, 6000000222));
        return handler;
    }

    /// <summary>Creates a handler that answers with both issues and then reports that the child has no parent.</summary>
    public static TestHttpMessageHandler Unparented()
    {
        var handler = Issues();
        handler.EnqueueJson(HttpStatusCode.NotFound, "{}");
        return handler;
    }

    /// <summary>Returns the JSON of an issue with the given number and native identifier.</summary>
    public static string Issue(int number, long id) =>
        string.Create(CultureInfo.InvariantCulture, $"{{\"id\":{id},\"number\":{number},\"url\":\"{ApiRoot}{number}\"}}");

    /// <summary>Runs <c>github link-subissue</c> with the given handler and values, and returns its exit code, output, and error.</summary>
    public static async Task<(int ExitCode, string Output, string Error)> Run(
        HttpMessageHandler handler,
        bool force = true,
        string repository = Repository,
        string parent = "10",
        string child = "20",
        CancellationToken? cancellationToken = null)
    {
        using var client = new HttpClient(handler, disposeHandler: false);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        List<string> arguments = ["github", "link-subissue", "--repository", repository, "--parent", parent, "--child", child];
        if (force)
        {
            arguments.Add("--force");
        }

        var exitCode = await RepoConfigToolRunner.Run(arguments, output, error, Path.GetTempPath(), client,
            cancellationToken ?? TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }
}
