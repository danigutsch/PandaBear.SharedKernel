using System.Runtime.CompilerServices;
using Aspire.Hosting.Testing;
using PandaBear.IntegrationTesting;

namespace PandaBear.EventSourcing.Npgsql.Tests;

/// <summary>Creates the Aspire builder in this test project's assembly.</summary>
public sealed class PostgreSqlTestServerFixture : PandaBear.Npgsql.Testing.PostgreSqlTestServerFixture
{
    // Aspire locates generated DCP metadata by walking the synchronous builder-creation stack.
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected override IDistributedApplicationTestingBuilder CreateBuilder() => AspireTestApplication.CreateBuilder();
}
