using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SharedKernel.IntegrationTesting;
using SharedKernel.IO;
using SharedKernel.Processes;

namespace SharedKernel.Npgsql.Testing;

/// <summary>Captures and atomically restores bounded logical archives of disposable PostgreSQL test databases.</summary>
public sealed class PostgreSqlTestArchiveClient
{
    private const int MaxDiagnosticBytes = 64 * 1024;
    private readonly Func<string, PostgreSqlTestDatabaseLease, ProcessStartInfo> _createCommand;
    private readonly int _maxArchiveBytes;
    private readonly PostgreSqlTestArchiveProcess _run;

    /// <summary>Uses pinned native clients and a size limit selected by the fixture owner.</summary>
    /// <param name="createCommand">
    /// Creates a fresh start info for the named native tool and supplied database lease. Set authentication and the
    /// effective role as appropriate for the caller's private test server. Use the argument list for any command prefix
    /// and keep passwords out of command arguments. The client appends its archive options to that argument list.
    /// </param>
    /// <param name="maxArchiveBytes">
    /// Bounds the export file and the verified in-memory input supplied to the native listing and restore.
    /// </param>
    /// <param name="processTimeout">The time limit for each native command.</param>
    /// <exception cref="ArgumentNullException"><paramref name="createCommand"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An archive or process limit is not positive.</exception>
    public PostgreSqlTestArchiveClient(
        Func<string, PostgreSqlTestDatabaseLease, ProcessStartInfo> createCommand,
        int maxArchiveBytes,
        TimeSpan processTimeout)
        : this(createCommand, maxArchiveBytes, UseProcessRunner(processTimeout))
    {
    }

    /// <summary>Uses a caller-supplied process transfer while testing the archive contract.</summary>
    internal PostgreSqlTestArchiveClient(
        Func<string, PostgreSqlTestDatabaseLease, ProcessStartInfo> createCommand,
        int maxArchiveBytes,
        PostgreSqlTestArchiveProcess run)
    {
        ArgumentNullException.ThrowIfNull(createCommand);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxArchiveBytes);
        ArgumentNullException.ThrowIfNull(run);
        _createCommand = createCommand;
        _maxArchiveBytes = maxArchiveBytes;
        _run = run;
    }

    /// <summary>Captures a custom-format dump and lists the exact verified bytes.</summary>
    /// <param name="source">A live source database lease owned by the caller.</param>
    /// <param name="workspace">The private test workspace that owns the archive file and its later cleanup.</param>
    /// <param name="ct">Cancels native execution and archive verification.</param>
    /// <returns>The archive identity and bounded native table of contents.</returns>
    /// <exception cref="ArgumentNullException">A necessary caller-owned input is null.</exception>
    /// <exception cref="InvalidDataException">The native dump is empty or its archive cannot be verified.</exception>
    /// <exception cref="InvalidOperationException">A native command could not complete within its output limit or returned an error.</exception>
    /// <exception cref="TimeoutException">A native command exceeded its configured time limit.</exception>
    /// <exception cref="IOException">The archive file cannot be written, read, or removed.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    public async Task<PostgreSqlTestArchive> Capture(
        PostgreSqlTestDatabaseLease source,
        TestWorkspace workspace,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(workspace);
        ct.ThrowIfCancellationRequested();
        var name = $"postgresql-{Guid.NewGuid():N}.dump";
        var path = Path.Combine(workspace.Root, name);
        var created = false;
        try
        {
            long length;
            var dump = CreateCommand(source, "pg_dump", "--format=custom", "--no-password", $"--dbname={source.DatabaseName}");
            await using (var output = OpenArchive(path))
            {
                created = true;
                await Run(dump, "pg_dump", input: null, output, _maxArchiveBytes, ct);
                length = output.Length;
            }

            if (length == 0)
            {
                throw new InvalidDataException("The native PostgreSQL dump returned an empty archive.");
            }

            string digest;
            await using (var file = File.OpenRead(path))
            {
                digest = Convert.ToHexString(await SHA256.HashDataAsync(file, ct));
            }

            var input = await VerifiedArtifact.Read(workspace.Root, name, digest, _maxArchiveBytes, ct);
            var list = CreateCommand(source, "pg_restore", "--list");
            using var contents = new MemoryStream();
            await Run(list, "pg_restore --list", input, contents, MaxDiagnosticBytes, ct);
            var table = Encoding.UTF8.GetString(contents.GetBuffer(), 0, (int)contents.Length);
            return new PostgreSqlTestArchive(path, digest, length, table, source.DatabaseName);
        }
        catch (Exception failure) when (created)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                throw new AggregateException("Cleaning the incomplete PostgreSQL archive also failed.", failure, cleanup);
            }

            throw;
        }
    }

    /// <summary>Restores the verified archive into a distinct caller-owned lease in one transaction.</summary>
    /// <param name="archive">The receipt from the private capture workspace.</param>
    /// <param name="target">A distinct, prepared test database whose roles and database ACLs the caller owns.</param>
    /// <param name="ct">Cancels archive verification and native execution.</param>
    /// <exception cref="ArgumentNullException">The archive or target is null.</exception>
    /// <exception cref="ArgumentException">The target is the source database.</exception>
    /// <exception cref="InvalidDataException">The archive exceeds the configured bound or its digest cannot be verified.</exception>
    /// <exception cref="InvalidOperationException">The native command could not complete within its output limits or returned an error.</exception>
    /// <exception cref="TimeoutException">The native restore exceeded its configured time limit.</exception>
    /// <exception cref="IOException">The archive file could not be read.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    public async Task Restore(PostgreSqlTestArchive archive, PostgreSqlTestDatabaseLease target, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(target);
        ct.ThrowIfCancellationRequested();
        if (string.Equals(archive.SourceDatabaseName, target.DatabaseName, StringComparison.Ordinal))
        {
            throw new ArgumentException("The PostgreSQL archive must not be restored into its source test database.", nameof(target));
        }

        var directory = Path.GetDirectoryName(archive.Path)
            ?? throw new InvalidDataException("A PostgreSQL archive must belong to its capture workspace.");
        var input = await VerifiedArtifact.Read(directory, Path.GetFileName(archive.Path), archive.Sha256, _maxArchiveBytes, ct);
        var restore = CreateCommand(
            target, "pg_restore", "--no-password", "--single-transaction", "--exit-on-error", $"--dbname={target.DatabaseName}");
        await Run(restore, "pg_restore", input, Stream.Null, MaxDiagnosticBytes, ct);
    }

    private ProcessStartInfo CreateCommand(PostgreSqlTestDatabaseLease database, string tool, params string[] arguments)
    {
        var command = _createCommand(tool, database);
        foreach (var argument in arguments)
        {
            command.ArgumentList.Add(argument);
        }

        return command;
    }

    private async Task Run(
        ProcessStartInfo command,
        string operation,
        ReadOnlyMemory<byte>? input,
        Stream output,
        int maxOutputBytes,
        CancellationToken ct)
    {
        var exitCode = await _run(command, input, output, maxOutputBytes, MaxDiagnosticBytes, ct);
        if (exitCode != 0)
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"{operation} exited with code {exitCode}."));
        }
    }

    private static PostgreSqlTestArchiveProcess UseProcessRunner(TimeSpan timeout)
    {
        var runner = new BoundedProcessRunner(timeout);
        return async (command, input, output, maxOutputBytes, maxErrorBytes, ct) =>
        {
            if (input is not { } bytes)
            {
                return await runner.RunToStreams(command, output, maxOutputBytes, Stream.Null, maxErrorBytes, ct);
            }

            var result = await runner.Capture(command, bytes, maxOutputBytes, maxErrorBytes, ct);
            await output.WriteAsync(result.StandardOutput, ct);
            return result.ExitCode;
        };
    }

    private static FileStream OpenArchive(string path)
    {
        var options = new FileStreamOptions
        {
            Access = FileAccess.Write,
            Mode = FileMode.CreateNew,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return new FileStream(path, options);
    }
}
