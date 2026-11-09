using System.Text.RegularExpressions;

namespace PandaBear.CI.Tool;

/// <summary>Recognizes the output of a child process that a sandbox stopped from creating a Unix domain socket.</summary>
internal static partial class SocketRefusal
{
    /// <summary>The explanation that replaces the raw output of a child process whose socket was refused.</summary>
    public const string Message =
        "The sandbox blocks Unix domain sockets, which this step needs. Run `sharedkernel-sandbox doctor` to see what the sandbox allows.";

    /// <summary>
    /// Reports whether <paramref name="output"/> shows a refused socket: a <c>SocketException</c> with errno 13 (EACCES) or 1 (EPERM), or
    /// <c>Permission denied</c> on the same line as the word socket or pipe.
    /// </summary>
    public static bool IsRefused(string output) =>
        SocketErrno().IsMatch(output) || PermissionDenied().IsMatch(output);

    [GeneratedRegex(@"SocketException \((13|1)\)", RegexOptions.CultureInvariant)]
    private static partial Regex SocketErrno();

    [GeneratedRegex(@"(?im)^.*(?:\b(?:socket|pipe)\b.*Permission denied|Permission denied.*\b(?:socket|pipe)\b).*$", RegexOptions.CultureInvariant)]
    private static partial Regex PermissionDenied();
}
