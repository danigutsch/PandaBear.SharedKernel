using System.Diagnostics;

namespace PandaBear.Sandbox.Tool.Tests;

/// <summary>Tells tests whether a FIFO can be created, and creates one.</summary>
internal static class SpecialFilesProbe
{
    private const string Mkfifo = "/usr/bin/mkfifo";

    /// <summary>Gets a value indicating whether <c>mkfifo</c> exists on a Unix system.</summary>
    public static bool CanCreateFifo => !OperatingSystem.IsWindows() && File.Exists(Mkfifo);

    /// <summary>Creates a FIFO at <paramref name="path"/> and waits for <c>mkfifo</c> to finish.</summary>
    public static async Task CreateFifo(string path, CancellationToken cancellationToken)
    {
        using var process = Process.Start(new ProcessStartInfo(Mkfifo) { ArgumentList = { path }, UseShellExecute = false }) ?? throw new InvalidOperationException("mkfifo did not start.");
        await process.WaitForExitAsync(cancellationToken);
    }
}
