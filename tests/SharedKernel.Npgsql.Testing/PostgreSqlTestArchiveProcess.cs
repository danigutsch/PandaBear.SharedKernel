using System.Diagnostics;

namespace SharedKernel.Npgsql.Testing;

/// <summary>Runs a bounded native archive command with optional binary standard input.</summary>
internal delegate Task<int> PostgreSqlTestArchiveProcess(
    ProcessStartInfo command,
    ReadOnlyMemory<byte>? input,
    Stream output,
    int maxOutputBytes,
    int maxErrorBytes,
    CancellationToken ct);
