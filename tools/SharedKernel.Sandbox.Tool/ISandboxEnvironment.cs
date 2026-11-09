namespace PandaBear.Sandbox.Tool;

/// <summary>The operations the probes try. A fake replaces it in tests.</summary>
internal interface ISandboxEnvironment
{
    /// <summary>Gets the home directory, used to expand <c>~/</c> in configured paths.</summary>
    string HomeDirectory
    {
        get;
    }

    /// <summary>
    /// Gets the home directory that the account database lists for the current user, independent of <c>$HOME</c>, or
    /// <see langword="null"/> when it cannot be determined.
    /// </summary>
    string? AccountHomeDirectory
    {
        get;
    }

    /// <summary>Gets the temporary directory.</summary>
    string TempDirectory
    {
        get;
    }

    /// <summary>Gets the NuGet package folder, or <see langword="null"/> when it cannot be determined.</summary>
    string? NuGetPackagesDirectory
    {
        get;
    }

    /// <summary>Creates and closes a Unix domain socket.</summary>
    (ProbeOutcome Outcome, string Detail) CreateUnixSocket();

    /// <summary>Creates and closes a .NET named pipe server, which is a Unix domain socket on Linux and macOS.</summary>
    (ProbeOutcome Outcome, string Detail) CreateNamedPipe();

    /// <summary>Creates a small file in <paramref name="directory"/> and deletes it again.</summary>
    (ProbeOutcome Outcome, string Detail) WriteFile(string directory);

    /// <summary>Opens <paramref name="path"/> for reading; a directory is listed.</summary>
    (ProbeOutcome Outcome, string Detail) ReadPath(string path);
}
