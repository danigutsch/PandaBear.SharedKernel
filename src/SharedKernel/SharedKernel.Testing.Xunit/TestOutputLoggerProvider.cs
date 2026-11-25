using Microsoft.Extensions.Logging;
using Xunit;

namespace SharedKernel.Testing;

/// <summary>
/// An <see cref="ILoggerProvider"/> that writes each entry to the output of the xUnit test it belongs to, never to the process console:
/// the runner shows a test's output only when the test fails, while <c>dotnet test</c> streams what the test process writes to the
/// console for every test. An entry belongs to the test running where it is logged; an entry logged outside any test (on a server's
/// request threads, in background work) belongs to the test that created the provider, while that test runs. Any other entry goes to the
/// xUnit diagnostic messages of the context it is logged in, if there is one, which the runner shows only when they are switched on
/// (<c>--xunit-diagnostics on</c>).
/// </summary>
/// <param name="owner">The output of the test that entries logged outside any test belong to, or <see langword="null"/> for none.</param>
public sealed class TestOutputLoggerProvider(ITestOutputHelper? owner) : ILoggerProvider
{
    /// <summary>Initializes a provider owned by the test that creates it, if one is running.</summary>
    public TestOutputLoggerProvider()
        : this(TestContext.Current.TestOutputHelper)
    {
    }

    /// <summary>Creates a logger that writes entries of <paramref name="categoryName"/> to the output of the test they belong to.</summary>
    /// <param name="categoryName">The category of the logger.</param>
    /// <returns>The logger.</returns>
    public ILogger CreateLogger(string categoryName) => new TestOutputLogger(categoryName, owner);

    /// <summary>Releases no resources.</summary>
    public void Dispose()
    {
    }
}
