using System.Runtime.CompilerServices;
using Aspire.Hosting.Testing;

namespace PandaBear.Npgsql.Tests;

/// <summary>
/// A fixture whose Aspire builder cannot be created: it counts every attempt and fails it, so a test that makes the
/// fixture reach for a server of its own cannot start a container.
/// </summary>
/// <param name="readEnvironment">Stands in for the process environment.</param>
internal sealed class RefusingServerFixture(Func<string, string?> readEnvironment) : Testing.PostgreSqlTestServerFixture(readEnvironment)
{
    private int _builderCalls;

    /// <summary>Gets how often the fixture tried to create its Aspire builder.</summary>
    public int BuilderCalls => Volatile.Read(ref _builderCalls);

    /// <summary>Gets the failure that every attempt throws.</summary>
    public InvalidOperationException BuilderFailure
    {
        get;
    } = new("The Aspire builder is refused in this test.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected override IDistributedApplicationTestingBuilder CreateBuilder()
    {
        _ = Interlocked.Increment(ref _builderCalls);
        throw BuilderFailure;
    }
}
