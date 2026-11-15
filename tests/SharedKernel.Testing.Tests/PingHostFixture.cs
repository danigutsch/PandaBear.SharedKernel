using Microsoft.Extensions.DependencyInjection;
using PandaBear.Testing.Playwright;

namespace PandaBear.Testing.Tests;

/// <summary>One run of the test application in the <c>Staging</c> environment, shared by the tests of a class, that counts its starts.</summary>
public sealed class PingHostFixture : KestrelHostFixture<Program>
{
    /// <summary>Gets the object registered in the shared application.</summary>
    public HostOwner Owner { get; } = new();

    /// <summary>Gets how many times the fixture started the application.</summary>
    public int Starts { get; private set; }

    /// <inheritdoc />
    protected override string EnvironmentName => "Staging";

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, string?>? Configuration => new Dictionary<string, string?> { ["Setting"] = "shared" };

    /// <inheritdoc />
    public override async ValueTask InitializeAsync()
    {
        Starts++;
        await base.InitializeAsync();
    }

    /// <inheritdoc />
    protected override void ConfigureServices(IServiceCollection services) => services.AddSingleton(Owner);
}
