using Aspire.Hosting.Testing;
using Xunit;

namespace SharedKernel.Aspire.Hosting.Umami.Testing;

/// <summary>Gives the tests of an assembly one Umami website, resolved on demand.</summary>
/// <remarks>
/// <para>
/// Creating and initializing the fixture starts nothing, so an assembly whose selected tests need no Umami pays for no container. The
/// first test that needs the website resolves it: the website in the Umami that another process owns when the
/// <c>SHAREDKERNEL_TESTING_UMAMI_CONNECTION_STRING</c> environment variable carries its connection string, otherwise a website in an
/// Umami with a PostgreSQL server of its own that <see cref="CreateBuilder"/> composes and the fixture starts once and later stops. A set
/// variable always borrows: a value that is no website connection string, or the owner's <c>Unavailable</c>, fails every test that needs
/// the website with a message that names the variable, and never falls back to a second Umami.
/// </para>
/// <para>A failed start is cached, so it fails every test that needs the website instead of repeating a long startup timeout.</para>
/// </remarks>
public abstract class UmamiTestWebsiteFixture : IAsyncLifetime
{
    private readonly UmamiTestWebsiteSource _source;

    /// <summary>Creates a fixture that finds a website another process owns in the process environment.</summary>
    protected UmamiTestWebsiteFixture()
    {
        _source = new UmamiTestWebsiteSource(ct => UmamiTestWebsite.Start(CreateBuilder(), ct));
    }

    /// <summary>Creates the testing builder from the consuming Aspire AppHost assembly.</summary>
    /// <returns>A builder with the consuming assembly's Aspire metadata.</returns>
    /// <remarks>
    /// Override it in the executable test assembly with <see cref="System.Runtime.CompilerServices.MethodImplOptions.NoInlining"/>. It runs
    /// only when this fixture starts an Umami of its own, on the thread of the first test that needs the website.
    /// </remarks>
    protected abstract IDistributedApplicationTestingBuilder CreateBuilder();

    /// <inheritdoc />
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>Gets the website, resolving it on first use.</summary>
    /// <param name="ct">Abandons only this caller's wait.</param>
    /// <returns>The website.</returns>
    internal Task<UmamiTestWebsiteConnection> GetWebsite(CancellationToken ct) => _source.Get(ct);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await _source.DisposeAsync().ConfigureAwait(false);
    }
}
