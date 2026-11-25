namespace SharedKernel.Secrets;

/// <summary>
/// One child-process run: an absolute executable, its arguments, the only environment variables it receives, and the bytes
/// for its standard input. Secrets and plaintext belong in <see cref="StandardInput"/>; a request that carries age key
/// material in an argument or an environment value is refused.
/// </summary>
public sealed class ToolRequest
{
    /// <summary>Creates a request.</summary>
    /// <param name="executable">The absolute path of the program.</param>
    /// <param name="arguments">The arguments, passed one by one without a shell.</param>
    /// <param name="environment">The environment variables to set; the child inherits nothing else except <c>LC_ALL=C</c>.</param>
    /// <param name="standardInput">The bytes written to the child's standard input before it is closed.</param>
    /// <param name="workingDirectory">The absolute directory the child starts in, or <see langword="null"/> for the caller's directory. Programs that read configuration from the working directory (<c>sops</c> reads <c>.sops.yaml</c>) must get a controlled one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="executable"/> or <paramref name="arguments"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The executable or the working directory is not absolute, or an argument or environment value looks like age key material.</exception>
    public ToolRequest(
        string executable,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        ReadOnlyMemory<byte> standardInput = default,
        string? workingDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(arguments);
        if (!Path.IsPathFullyQualified(executable))
        {
            throw new ArgumentException("The executable must be an absolute path.", nameof(executable));
        }

        if (arguments.Any(LooksLikeKeyMaterial) || (environment?.Values.Any(LooksLikeKeyMaterial) ?? false))
        {
            throw new ArgumentException("Key material must never be passed in an argument or an environment variable.", nameof(arguments));
        }

        if (workingDirectory is not null && !Path.IsPathFullyQualified(workingDirectory))
        {
            throw new ArgumentException("The working directory must be an absolute path.", nameof(workingDirectory));
        }

        WorkingDirectory = workingDirectory;
        Executable = executable;
        Arguments = [.. arguments];
        Environment = environment is null ? new Dictionary<string, string>() : new Dictionary<string, string>(environment, StringComparer.Ordinal);
        StandardInput = standardInput;
    }

    /// <summary>Gets the absolute path of the program.</summary>
    public string Executable
    {
        get;
    }

    /// <summary>Gets the arguments.</summary>
    public IReadOnlyList<string> Arguments
    {
        get;
    }

    /// <summary>Gets the environment variables that the child receives.</summary>
    public IReadOnlyDictionary<string, string> Environment
    {
        get;
    }

    /// <summary>Gets the absolute working directory of the child, or <see langword="null"/> for the caller's.</summary>
    public string? WorkingDirectory
    {
        get;
    }

    /// <summary>Gets the bytes for standard input.</summary>
    public ReadOnlyMemory<byte> StandardInput
    {
        get;
    }

    private static bool LooksLikeKeyMaterial(string value) =>
        value.Contains("AGE-SECRET-KEY-", StringComparison.OrdinalIgnoreCase) || value.Contains("AGE-PLUGIN-", StringComparison.OrdinalIgnoreCase);
}
