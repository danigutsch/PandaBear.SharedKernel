using Microsoft.Extensions.Time.Testing;
using PandaBear.Results;

namespace PandaBear.Packages.Tests;

/// <summary>Creates <see cref="UpdateSources"/> over fakes and records whether they were created and disposed.</summary>
internal sealed class CountingSources(IAdvisorySource advisories, FakeRegistrySource registry) : IDisposable
{
    /// <summary>Gets how many times the sources were created.</summary>
    public int Created { get; private set; }

    /// <summary>Gets whether the created sources were disposed.</summary>
    public bool Disposed { get; private set; }

    /// <summary>Creates sources over fakes that know bat, fd-find and corepack and report no advisories.</summary>
    public static CountingSources Default(Result<IReadOnlyList<Advisory>>? advisories = null, string[]? notFound = null) => new(
        new FakeAdvisorySource(advisories ?? Result.Ok<IReadOnlyList<Advisory>>([])),
        new FakeRegistrySource(
            new()
            {
                ["bat"] = "0.26.1",
                ["fd-find"] = "10.0.0",
                ["corepack"] = "0.40.0"
            },
            new()
            {
                ["bat"] = PlannerFake.Now.AddDays(-30),
                ["fd-find"] = PlannerFake.Now.AddDays(-2),
                ["corepack"] = PlannerFake.Now.AddDays(-30)
            },
            notFound: notFound));

    /// <summary>Creates the sources; disposing them sets <see cref="Disposed"/>.</summary>
    public UpdateSources Create()
    {
        Created++;
        return new UpdateSources(advisories, registry, registry, new FakeTimeProvider(PlannerFake.Now), this);
    }

    /// <inheritdoc />
    public void Dispose() => Disposed = true;
}
