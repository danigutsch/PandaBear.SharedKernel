using System.Runtime.CompilerServices;
using Aspire.Hosting.Testing;
using PandaBear.IntegrationTesting;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Leases isolated PostgreSQL databases for this test assembly on a server that the shared fixture borrows from the run
/// owner or starts on first demand, so that a class which needs no database starts no container.
/// </summary>
public sealed class PostgreSqlFixture : PostgreSqlTestServerFixture
{
    /// <summary>
    /// Leases a new isolated PostgreSQL database from the shared server.
    /// </summary>
    internal Task<PostgreSqlTestDatabaseLease> CreateIsolatedDatabase(CancellationToken ct) => CreateDatabase(ct);

    // Aspire locates generated DCP metadata by walking the synchronous builder-creation stack.
    /// <summary>
    /// Creates the distributed application testing builder through AspireTestApplication.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected override IDistributedApplicationTestingBuilder CreateBuilder() => AspireTestApplication.CreateBuilder();
}
