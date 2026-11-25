using SharedKernel.Testing.Playwright;

namespace SharedKernel.Testing.Tests;

/// <summary>A browser test that is never started, so tests can read what it exposes before it starts.</summary>
internal sealed class KestrelPageTestProbe : KestrelPageTest<Program>
{
    /// <summary>Initializes a probe that would start an application of its own.</summary>
    public KestrelPageTestProbe()
    {
    }

    /// <summary>Initializes a probe that would use a shared application.</summary>
    /// <param name="sharedHost">The shared application.</param>
    public KestrelPageTestProbe(KestrelHostFixture<Program> sharedHost)
        : base(sharedHost)
    {
    }

    /// <summary>Gets <see cref="KestrelPageTest{TEntryPoint}.BaseUri"/>.</summary>
    public Uri Address => BaseUri;

    /// <summary>Gets <see cref="KestrelPageTest{TEntryPoint}.Services"/>.</summary>
    public IServiceProvider ApplicationServices => Services;
}
