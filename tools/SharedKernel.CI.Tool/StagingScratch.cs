using System.Buffers;

namespace PandaBear.CI.Tool;

/// <summary>
/// A directory beside the output, on the same volume, where packages are verified before they move into the output. Its
/// name is <c>.&lt;output name&gt;.stage-&lt;32 hexadecimal characters&gt;</c>, so a later run can recognize one that an
/// interrupted run left behind.
/// </summary>
internal sealed class StagingScratch : IDisposable
{
    /// <summary>
    /// The age after which a staging directory is left over from an interrupted run. A running stager adds files to its
    /// directory far more often, because every request times out within minutes.
    /// </summary>
    public static readonly TimeSpan StaleAge = TimeSpan.FromDays(1);

    private static readonly SearchValues<char> LowercaseHexadecimal = SearchValues.Create("0123456789abcdef");

    private StagingScratch(string fullPath) => FullPath = fullPath;

    /// <summary>Gets the absolute directory path.</summary>
    public string FullPath
    {
        get;
    }

    /// <summary>Deletes the staging directories of <paramref name="output"/> that are older than <see cref="StaleAge"/>.</summary>
    /// <remarks>
    /// A symbolic link with a staging name is left alone; the sweep never follows one. A directory that cannot be read or
    /// deleted stays for the sweep of a later run.
    /// </remarks>
    public static void SweepStale(string output, TimeProvider timeProvider)
    {
        var parent = Parent(output);
        if (!Directory.Exists(parent))
        {
            return;
        }

        var prefix = Prefix(output);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var directory in new DirectoryInfo(parent).EnumerateDirectories(prefix + "*"))
        {
            try
            {
                if (directory.Name.Length == prefix.Length + 32
                    && !directory.Name.AsSpan(prefix.Length).ContainsAnyExcept(LowercaseHexadecimal)
                    && directory.LinkTarget is null
                    && now - directory.LastWriteTimeUtc > StaleAge)
                {
                    directory.Delete(recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A run that starts at the same time may delete the same leftover first. Neither that nor a leftover that
                // cannot be deleted stops this run, which moves packages only from its own staging directory.
            }
        }
    }

    /// <summary>Creates an empty staging directory beside <paramref name="output"/>, creating the output's parent when needed.</summary>
    public static StagingScratch Create(string output)
    {
        var parent = Parent(output);
        Directory.CreateDirectory(parent);
        return new StagingScratch(Directory.CreateDirectory(Path.Combine(parent, Prefix(output) + Guid.NewGuid().ToString("N"))).FullName);
    }

    /// <summary>Deletes the staging directory; a failure leaves it for the sweep of a later run.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(FullPath, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The sweep of a later run removes it once it is stale.
        }
    }

    private static string Parent(string output) =>
        Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(output))
            ?? throw new InvalidOperationException($"The output directory needs a parent directory: {output}.");

    private static string Prefix(string output) => "." + Path.GetFileName(Path.TrimEndingDirectorySeparator(output)) + ".stage-";
}
