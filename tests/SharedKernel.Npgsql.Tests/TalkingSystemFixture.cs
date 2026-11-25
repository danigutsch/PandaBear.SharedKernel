using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using SharedKernel.IntegrationTesting;

namespace SharedKernel.Npgsql.Tests;

/// <summary>A system fixture whose profiles each run one talking process, recording the folder each profile was given.</summary>
internal sealed class TalkingSystemFixture : AspireSystemFixture
{
    private readonly ConcurrentBag<string> _folders = [];

    /// <summary>Gets the folders the profiles were started with.</summary>
    internal IReadOnlyCollection<string> Folders => _folders;

    /// <inheritdoc />
    protected override string WorkspaceName => "sk-system-fixture";

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected override Task<AspireTestApplication> Start(string profile, string folder, CancellationToken ct)
    {
        _folders.Add(folder);
        var builder = AspireTestApplication.CreateBuilder();
        _ = TalkingProcess.Add(builder, profile, "started " + profile, healthy: true);
        return AspireTestApplication.Start(builder, [profile], TimeSpan.FromMinutes(1), ct);
    }
}
