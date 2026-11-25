using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Testing.Playwright;

namespace SharedKernel.Testing.Tests;

/// <summary>A browser test that starts the test application for itself, with a setting and an object of its own registered in it.</summary>
public abstract class OwnHostPageTest : KestrelPageTest<Program>
{
    /// <summary>Gets the object this test registers in its application.</summary>
    protected HostOwner Owner { get; } = new();

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, string?>? Configuration => new Dictionary<string, string?> { ["Setting"] = "own" };

    /// <inheritdoc />
    protected override void ConfigureServices(IServiceCollection services) => services.AddSingleton(Owner);
}
