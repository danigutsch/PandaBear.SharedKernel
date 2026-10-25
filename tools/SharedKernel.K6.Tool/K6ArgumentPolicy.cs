namespace PandaBear.K6.Tool;

/// <summary>Rejects k6 arguments that would widen what a run can read, send, or write.</summary>
internal static class K6ArgumentPolicy
{
    private static readonly string[] OutputOptions = ["--out", "-o", "--log-output", "--console-output", "--traces-output"];

    /// <summary>Validates the arguments that reach <c>k6 run</c>.</summary>
    /// <param name="arguments">The arguments, in order.</param>
    /// <param name="controlledVariables">The k6 variables that the tool sets, which <c>-e</c> and <c>--env</c> cannot set.</param>
    /// <param name="allowHttpDebug">Whether <c>--http-debug</c> is allowed.</param>
    /// <param name="allowRemoteOutput">Whether the k6 output options are allowed.</param>
    /// <exception cref="InvalidOperationException">An argument is not allowed.</exception>
    public static void Validate(
        IReadOnlyList<string> arguments,
        IReadOnlyCollection<string> controlledVariables,
        bool allowHttpDebug,
        bool allowRemoteOutput)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];

            // k6 reads -eNAME=value, -e=NAME=value, and bundles such as -qo json as options, so a short option stands
            // alone and the checks below see its value.
            if (argument.Length > 2 && argument[0] == '-' && char.IsAsciiLetter(argument[1]))
            {
                throw new InvalidOperationException($"Pass k6 short options on their own, with any value as the next argument: {argument}");
            }

            if (Is(argument, "--include-system-env-vars"))
            {
                throw new InvalidOperationException("--include-system-env-vars is not allowed: it passes the whole k6 environment to the script.");
            }

            if (Is(argument, "--insecure-skip-tls-verify"))
            {
                throw new InvalidOperationException("--insecure-skip-tls-verify is not allowed.");
            }

            if (Is(argument, "--summary-export"))
            {
                throw new InvalidOperationException("--summary-export is not allowed: the tool writes k6-summary.json to the directory of the run.");
            }

            if (!allowHttpDebug && Is(argument, "--http-debug"))
            {
                throw new InvalidOperationException("--http-debug can expose request and response data. Pass --allow-http-debug for explicit local debugging.");
            }

            if (!allowRemoteOutput && OutputOptions.Any(option => Is(argument, option)))
            {
                throw new InvalidOperationException("Custom k6 outputs are disabled by default. Pass --allow-remote-output after reviewing the output destination and credentials.");
            }

            var assignment = argument switch
            {
                "-e" or "--env" => index + 1 < arguments.Count ? arguments[index + 1] : null,
                _ when argument.StartsWith("--env=", StringComparison.Ordinal) => argument["--env=".Length..],
                _ => null,
            };
            var name = assignment?.Split('=', 2)[0];
            if (name is not null && controlledVariables.Contains(name, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"-e and --env cannot set {name}, which the tool sets from the scenario configuration.");
            }
        }
    }

    private static bool Is(string argument, string option) =>
        string.Equals(argument, option, StringComparison.Ordinal)
        || argument.StartsWith(option + "=", StringComparison.Ordinal);
}
