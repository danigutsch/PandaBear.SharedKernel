using System.Runtime.CompilerServices;
using Aspire.Hosting.Testing;
using SharedKernel.IntegrationTesting;

namespace SharedKernel.EventSourcing.Npgsql.Tests;

/// <summary>Creates the Aspire builder in this test project's assembly.</summary>
public sealed class PostgreSqlTestServerFixture : SharedKernel.Npgsql.Testing.PostgreSqlTestServerFixture
{
    /// <summary>Creates the Aspire testing builder from this assembly, never inlined so that Aspire finds this assembly's DCP metadata on the call stack.</summary>
    // Aspire locates generated DCP metadata by walking the synchronous builder-creation stack.
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected override IDistributedApplicationTestingBuilder CreateBuilder() => AspireTestApplication.CreateBuilder();
}
