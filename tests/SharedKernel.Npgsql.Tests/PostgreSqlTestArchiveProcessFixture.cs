using System.Diagnostics;

namespace SharedKernel.Npgsql.Tests;

/// <summary>Records native command requests and simulates bounded binary stdout and stdin for the archive client tests.</summary>
internal sealed class PostgreSqlTestArchiveProcessFixture(byte[] dump)
{
    /// <summary>Gets the synthetic authentication value that diagnostics must not expose.</summary>
    internal const string Secret = "fixture-native-password";

    /// <summary>Gets the native commands constructed by the archive client in request order.</summary>
    public List<ProcessStartInfo> Commands { get; } = [];

    /// <summary>Gets the archive size limit supplied to the dump process.</summary>
    public int DumpLimit { get; private set; }

    /// <summary>Gets the exact bytes sent to the native restore.</summary>
    public ReadOnlyMemory<byte> RestoreInput { get; private set; }

    /// <summary>Gets or sets the simulated exit code for the native restore.</summary>
    public int RestoreExitCode { get; set; }

    /// <summary>Creates a command with a prefix that stands in for an owned native client invocation.</summary>
    public ProcessStartInfo Start(string tool, PostgreSqlTestDatabaseLease database)
    {
        var command = new ProcessStartInfo(Path.GetFullPath("fake-native-client"));
        command.ArgumentList.Add(tool);
        command.Environment["PGPASSWORD"] = Secret;
        command.Environment["PGDATABASE"] = database.DatabaseName;
        Commands.Add(command);
        return command;
    }

    /// <summary>Simulates the native process transferring the requested bytes within its declared limits.</summary>
    public async Task<int> Run(
        ProcessStartInfo command,
        ReadOnlyMemory<byte>? input,
        Stream output,
        int maxOutputBytes,
        int maxErrorBytes,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxErrorBytes);
        switch (command.ArgumentList[0])
        {
            case "pg_dump":
                DumpLimit = maxOutputBytes;
                var length = Math.Min(dump.Length, maxOutputBytes);
                await output.WriteAsync(dump.AsMemory(0, length), ct);
                if (length != dump.Length)
                {
                    throw new InvalidOperationException("The native archive exceeded its output limit.");
                }

                return 0;
            case "pg_restore" when command.ArgumentList.Contains("--list"):
                await output.WriteAsync("public fixture table\n"u8.ToArray(), ct);
                return 0;
            case "pg_restore":
                RestoreInput = input.GetValueOrDefault();
                return RestoreExitCode;
            default:
                throw new InvalidOperationException("Unexpected native command.");
        }
    }
}
