using System.Runtime.CompilerServices;
using Aspire.Hosting.Testing;
using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Tests;

/// <summary>Creates the Aspire builder in this test project's assembly.</summary>
public sealed class PostgreSqlTestServerFixture : Testing.PostgreSqlTestServerFixture
{
    /// <summary>Creates the Aspire testing builder from this assembly, never inlined so that Aspire finds this assembly's DCP metadata on the call stack.</summary>
    // Aspire locates generated DCP metadata by walking the synchronous builder-creation stack.
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected override IDistributedApplicationTestingBuilder CreateBuilder() => AspireTestApplication.CreateBuilder();
}
