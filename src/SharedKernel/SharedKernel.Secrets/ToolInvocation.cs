using PandaBear.Results;

namespace PandaBear.Secrets;

/// <summary>Checks file arguments and builds failed results whose message never carries file content.</summary>
internal static class ToolInvocation
{
    /// <summary>Checks that <paramref name="path"/> is an absolute file path that is safe to pass as an argument.</summary>
    internal static string? CheckFile(string path) => path switch
    {
        null or "" => "The file path is required.",
        _ when !Path.IsPathFullyQualified(path) => "The file path must be absolute.",
        _ when path.Any(char.IsControl) => "The file path cannot contain control characters.",
        _ => null,
    };

    /// <summary>Creates a failed result with <paramref name="problem"/> as its detail.</summary>
    internal static Result<T> Error<T>(string problem)
        where T : notnull => Result.Error<T>(problem);
}
