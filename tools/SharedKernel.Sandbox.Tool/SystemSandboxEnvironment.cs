using System.IO.Pipes;
using System.Net.Sockets;

namespace PandaBear.Sandbox.Tool;

/// <summary>The real environment: every operation is tried and undone, and no detail carries a path.</summary>
internal sealed class SystemSandboxEnvironment : ISandboxEnvironment
{
    private const int FolderEntriesChecked = 20;
    private const int ExchangeTimeoutMilliseconds = 2000;
    private static readonly TimeSpan ReadLimit = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <inheritdoc />
    public string TempDirectory => Path.GetTempPath();

    /// <inheritdoc />
    public string? NuGetPackagesDirectory
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            return !string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured)
                ? configured
                : HomeDirectory.Length == 0 ? null : Path.Combine(HomeDirectory, ".nuget", "packages");
        }
    }

    /// <inheritdoc />
    public (ProbeOutcome Outcome, string Detail) CreateUnixSocket()
    {
        // What dotnet test and project-mode dotnet format do: create a socket, bind it to a path in the temporary folder, connect
        // a second socket to it, and exchange a byte. A sandbox can refuse any one of these stages.
        var stage = "Creating";
        var path = Path.Combine(TempDirectory, "sk" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            stage = "Binding";
            listener.Bind(new UnixDomainSocketEndPoint(path));
            listener.Listen(1);
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            client.ReceiveTimeout = ExchangeTimeoutMilliseconds;
            stage = "Connecting";
            client.Connect(new UnixDomainSocketEndPoint(path));
            using var accepted = listener.Accept();
            accepted.ReceiveTimeout = ExchangeTimeoutMilliseconds;
            stage = "Exchanging a byte over";
            client.Send([1]);
            var received = new byte[1];
            return accepted.Receive(received) == 1
                ? (ProbeOutcome.Allowed, "A Unix domain socket can be created, bound, connected, and used.")
                : (ProbeOutcome.Failed, "Exchanging a byte over a Unix domain socket returned nothing.");
        }
        catch (SocketException exception) when (exception.SocketErrorCode is SocketError.AccessDenied)
        {
            return (ProbeOutcome.Denied, $"{stage} a Unix domain socket was refused (permission denied){Hint(stage)}.");
        }
        catch (SocketException exception)
        {
            return (ProbeOutcome.Failed, $"{stage} a Unix domain socket failed with {exception.SocketErrorCode}.");
        }
        catch (ArgumentException)
        {
            return (ProbeOutcome.Failed, "The temporary folder path is too long for a socket path (about 100 bytes).");
        }
        catch (PlatformNotSupportedException)
        {
            return (ProbeOutcome.Skipped, "Unix domain sockets are not supported on this platform.");
        }
        finally
        {
            RemoveSocketFile(path);
        }
    }

    /// <inheritdoc />
    public (ProbeOutcome Outcome, string Detail) CreateNamedPipe()
    {
        var stage = "Creating";
        try
        {
            var name = "sk" + Guid.NewGuid().ToString("N")[..8];
            using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.None);
            using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.None);
            stage = "Connecting to";
            client.Connect(ExchangeTimeoutMilliseconds);
            server.WaitForConnection();
            stage = "Exchanging a byte over";
            client.WriteByte(1);
            client.Flush();
            return server.ReadByte() == 1
                ? (ProbeOutcome.Allowed, "A named pipe can be created, connected, and used.")
                : (ProbeOutcome.Failed, "Exchanging a byte over a named pipe returned nothing.");
        }
        catch (Exception exception) when (exception is SocketException { SocketErrorCode: SocketError.AccessDenied } or UnauthorizedAccessException)
        {
            return (ProbeOutcome.Denied, $"{stage} a named pipe was refused (permission denied){Hint(stage)}.");
        }
        catch (TimeoutException)
        {
            return (ProbeOutcome.Failed, "Connecting to a named pipe did not finish within two seconds.");
        }
        catch (ArgumentException)
        {
            // The socket path under the temporary folder is longer than the system allows (108 bytes on Linux, 104 on macOS).
            return (ProbeOutcome.Failed, "Creating a named pipe failed: the temporary folder path is too long for a socket path.");
        }
        catch (Exception exception) when (exception is IOException or SocketException or PlatformNotSupportedException)
        {
            return (ProbeOutcome.Failed, $"{stage} a named pipe failed.");
        }
    }

    // The stage that was refused points at the layer that probably refused it. This is inference, so it says "typically".
    private static string Hint(string stage) => stage switch
    {
        "Creating" => "; typically a filter on socket creation, such as seccomp",
        "Binding" => "; typically a rule on the path of the socket file; a temporary folder that the sandbox allows may help",
        "Connecting" or "Connecting to" => "; typically a rule on connecting to socket paths",
        _ => string.Empty,
    };

    private static void RemoveSocketFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing was bound if the probe was refused before binding.
        }
    }

    /// <inheritdoc />
    public (ProbeOutcome Outcome, string Detail) WriteFile(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return (ProbeOutcome.Skipped, "The folder does not exist.");
        }

        var path = Path.Combine(directory, ".sharedkernel-sandbox-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(path, "probe");
            return (ProbeOutcome.Allowed, "A file can be created there.");
        }
        catch (UnauthorizedAccessException)
        {
            return (ProbeOutcome.Denied, "Writing there was refused (permission denied).");
        }
        catch (IOException exception) when (exception.Message.Contains("Read-only file system", StringComparison.OrdinalIgnoreCase))
        {
            return (ProbeOutcome.Denied, "Writing there was refused (read-only file system).");
        }
        catch (IOException)
        {
            return (ProbeOutcome.Failed, "Writing there failed.");
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Nothing was created if the write was refused.
            }
        }
    }

    /// <inheritdoc />
    public (ProbeOutcome Outcome, string Detail) ReadPath(string path)
    {
        // Opening a FIFO blocks until a writer appears, and a synchronous read cannot be cancelled, so the read gets a limit.
        var read = Task.Run(() => ReadContent(path));
        return read.Wait(ReadLimit)
            ? read.Result
            : (ProbeOutcome.Skipped, "Reading did not answer within two seconds, as a FIFO or device would not; it cannot be probed this way.");
    }

    private static (ProbeOutcome Outcome, string Detail) ReadContent(string path)
    {
        // A sandbox can hide a path behind an empty folder or an empty file, so opening or listing it proves nothing: only
        // content that can be read counts as readable, and an empty result cannot be told apart from a path that holds nothing.
        const string Ambiguous = "Nothing could be read: the path is empty, hidden, or missing, and those cannot be told apart.";
        try
        {
            if (Directory.Exists(path))
            {
                return ReadFolder(path, Ambiguous);
            }

            return File.Exists(path) ? ReadFile(path, Ambiguous) : (ProbeOutcome.Skipped, Ambiguous);
        }
        catch (UnauthorizedAccessException)
        {
            return (ProbeOutcome.Denied, "Reading was refused (permission denied).");
        }
        catch (IOException)
        {
            // A socket or another special file cannot be opened like a file, which says nothing about whether it is protected.
            return (ProbeOutcome.Skipped, "The path could not be opened as a file, such as a socket; it cannot be probed this way.");
        }
    }

    private static (ProbeOutcome Outcome, string Detail) ReadFile(string path, string ambiguous)
    {
        using var stream = File.OpenRead(path);
        return stream.ReadByte() >= 0 ? (ProbeOutcome.Allowed, "Content of the file can be read.") : (ProbeOutcome.Skipped, ambiguous);
    }

    // A folder whose entries can be listed is only exposed when a file in it can be read: some sandboxes allow listing and
    // refuse reading, and then the contents are protected.
    private static (ProbeOutcome Outcome, string Detail) ReadFolder(string path, string ambiguous)
    {
        var entries = Directory.EnumerateFileSystemEntries(path).Take(FolderEntriesChecked).ToArray();
        if (entries.Length == 0)
        {
            return (ProbeOutcome.Skipped, ambiguous);
        }

        var sawFile = false;
        foreach (var entry in entries.Where(static e => File.Exists(e)))
        {
            sawFile = true;
            try
            {
                using var stream = File.OpenRead(entry);
                if (stream.ReadByte() >= 0)
                {
                    return (ProbeOutcome.Allowed, "Content of a file in the folder can be read.");
                }
            }
            catch (UnauthorizedAccessException)
            {
                return (ProbeOutcome.Denied, "The entries can be listed but a file in the folder cannot be read (permission denied).");
            }
            catch (IOException)
            {
                // A special file in the folder says nothing; the next entry may be a regular file.
            }
        }

        return sawFile
            ? (ProbeOutcome.Skipped, ambiguous)
            : (ProbeOutcome.Allowed, "The folder has entries that can be listed.");
    }
}
