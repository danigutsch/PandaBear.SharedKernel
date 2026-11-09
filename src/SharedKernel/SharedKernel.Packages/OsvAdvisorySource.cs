using System.Net.Http.Headers;
using System.Text.Json;
using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>
/// An <see cref="IAdvisorySource"/> backed by OSV (https://osv.dev). It asks <c>/v1/querybatch</c> which vulnerabilities
/// affect each exact package version, then reads each vulnerability from <c>/v1/vulns/{id}</c> for its severity.
/// OSV matches the version against the record's ranges with the ecosystem's own version order, so this library compares no
/// versions: an advisory lists the versions that were asked about and OSV reported as affected, plus any
/// <c>affected[].versions</c> the record lists. A record that gives only ranges is therefore still exact for every version
/// asked about, and it never claims a version that was not checked.
/// Any failed, oversized, unreadable, redirected, or partial answer fails the whole call. Withdrawn records are left out.
/// Answers are cached in memory for a limited time; failures are never cached.
/// </summary>
public sealed class OsvAdvisorySource : IAdvisorySource
{
    /// <summary>The largest response that is accepted, in bytes.</summary>
    public const int MaxResponseBytes = 8 * 1024 * 1024;

    /// <summary>The number of package versions asked about in one request.</summary>
    public const int BatchSize = 100;

    /// <summary>The most result pages that are followed for one batch.</summary>
    public const int MaxPages = 20;

    /// <summary>The most distinct vulnerabilities one call reads; more fails the call.</summary>
    public const int MaxVulnerabilities = 1000;

    /// <summary>The most versions taken from one record's <c>affected[].versions</c> list.</summary>
    public const int MaxListedVersions = 1000;

    private const string Host = "api.osv.dev";
    private static readonly Uri QueryBatchUri = new("https://api.osv.dev/v1/querybatch");

    private readonly HttpClient _client;
    private readonly string? _aptEcosystem;
    private readonly TimeSpan _timeout;
    private readonly ExpiringCache<string[]> _idCache;
    private readonly ExpiringCache<OsvRecord> _recordCache;

    /// <summary>Creates the source.</summary>
    /// <param name="client">The client; build its handler with <see cref="PackageHttp.CreateHandler"/>. The source does not dispose it.</param>
    /// <param name="timeProvider">The clock for cache expiry.</param>
    /// <param name="aptEcosystem">The OSV release for apt, such as <c>Debian:12</c>; apt lookups fail without it. OSV names Debian packages by source package, so a binary package with another name matches nothing.</param>
    /// <param name="cacheLifetime">How long an answer is reused; one hour when <see langword="null"/>.</param>
    /// <param name="requestTimeout">The time allowed for each request; 30 seconds when <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A time span is zero or negative.</exception>
    public OsvAdvisorySource(HttpClient client, TimeProvider timeProvider, string? aptEcosystem = null, TimeSpan? cacheLifetime = null, TimeSpan? requestTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _client = client;
        _aptEcosystem = aptEcosystem;
        _timeout = requestTimeout ?? TimeSpan.FromSeconds(30);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_timeout, TimeSpan.Zero, nameof(requestTimeout));
        var lifetime = cacheLifetime ?? TimeSpan.FromHours(1);
        _idCache = new ExpiringCache<string[]>(timeProvider, lifetime);
        _recordCache = new ExpiringCache<OsvRecord>(timeProvider, lifetime);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<Advisory>>> Query(
        PackageManagerKind manager,
        IReadOnlyCollection<InstalledPackage> packages,
        NetworkAccess access,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packages);
        if (access != NetworkAccess.Allowed)
        {
            return Result.Forbidden<IReadOnlyList<Advisory>>("The network is not allowed for this call; no advisory lookup was made.");
        }

        var ecosystem = OsvEcosystems.For(manager, _aptEcosystem);
        if (ecosystem.IsFailure)
        {
            return Result<IReadOnlyList<Advisory>>.FromError(ecosystem.ErrorDetails);
        }

        var pairs = packages.Distinct().OrderBy(static p => p.Name.Value, StringComparer.Ordinal).ThenBy(static p => p.Version.Value, StringComparer.Ordinal).ToArray();
        var idsByPair = new Dictionary<InstalledPackage, string[]>();
        List<InstalledPackage> misses = [];
        foreach (var pair in pairs)
        {
            if (_idCache.TryGet(IdKey(ecosystem.Value, pair), out var cached))
            {
                idsByPair[pair] = cached;
            }
            else
            {
                misses.Add(pair);
            }
        }

        foreach (var chunk in misses.Chunk(BatchSize))
        {
            var batch = await QueryBatch(ecosystem.Value, chunk, idsByPair, cancellationToken).ConfigureAwait(false);
            if (batch.IsFailure)
            {
                return Result<IReadOnlyList<Advisory>>.FromError(batch.ErrorDetails);
            }
        }

        var ids = idsByPair.Values.SelectMany(static v => v).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (ids.Length > MaxVulnerabilities)
        {
            return Result.Unavailable<IReadOnlyList<Advisory>>($"OSV reported more than {MaxVulnerabilities} distinct vulnerabilities; the lookup is refused rather than cut short.");
        }

        Dictionary<string, OsvRecord> records = new(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            var record = await GetRecord(id, cancellationToken).ConfigureAwait(false);
            if (record.IsFailure)
            {
                return Result<IReadOnlyList<Advisory>>.FromError(record.ErrorDetails);
            }

            records[id] = record.Value;
        }

        return Build(ecosystem.Value, idsByPair, records);
    }

    private static string IdKey(string ecosystem, InstalledPackage pair) => $"{ecosystem}\n{pair.Name.Value}\n{pair.Version.Value}";

    private static bool IsSafeId(string id) => id.Length is > 0 and <= Advisory.MaxIdLength && id.All(static c => char.IsAsciiLetterOrDigit(c) || c is ':' or '_' or '.' or '-');

    private static Result<IReadOnlyList<Advisory>> Build(string ecosystem, Dictionary<InstalledPackage, string[]> idsByPair, Dictionary<string, OsvRecord> records)
    {
        Dictionary<(string Id, PackageName Name), HashSet<PackageVersion>> affected = [];
        foreach (var (pair, ids) in idsByPair)
        {
            foreach (var id in ids)
            {
                var record = records[id];
                if (record.Withdrawn)
                {
                    continue;
                }

                if (!affected.TryGetValue((id, pair.Name), out var versions))
                {
                    versions = [];
                    affected[(id, pair.Name)] = versions;
                }

                versions.Add(pair.Version);
                if (record.VersionsByPackage.TryGetValue($"{ecosystem}\n{pair.Name.Value}", out var listed))
                {
                    foreach (var text in listed.Take(MaxListedVersions))
                    {
                        var version = PackageVersion.Create(text);
                        if (version.IsSuccess)
                        {
                            versions.Add(version.Value);
                        }
                    }
                }
            }
        }

        List<Advisory> advisories = [];
        foreach (var ((id, name), versions) in affected.OrderBy(static e => e.Key.Id, StringComparer.Ordinal).ThenBy(static e => e.Key.Name.Value, StringComparer.Ordinal))
        {
            var advisory = Advisory.Create(id, name, records[id].Severity, versions);
            if (advisory.IsFailure)
            {
                return Result<IReadOnlyList<Advisory>>.FromError(advisory.ErrorDetails);
            }

            advisories.Add(advisory.Value);
        }

        return Result.Ok<IReadOnlyList<Advisory>>(advisories);
    }

    private async Task<Result> QueryBatch(string ecosystem, InstalledPackage[] chunk, Dictionary<InstalledPackage, string[]> idsByPair, CancellationToken cancellationToken)
    {
        var found = chunk.Select(static _ => new List<string>()).ToArray();
        Dictionary<int, string> tokens = [];
        List<int> active = [.. Enumerable.Range(0, chunk.Length)];
        for (var page = 0; active.Count > 0; page++)
        {
            if (page >= MaxPages)
            {
                return Result.Unavailable($"OSV needed more than {MaxPages} result pages for one batch; the lookup is refused rather than cut short.");
            }

            var queries = active.Select(i => new OsvWire.Query(new OsvWire.PackageRef(chunk[i].Name.Value, ecosystem), chunk[i].Version.Value, tokens.GetValueOrDefault(i))).ToList();
            using var request = new HttpRequestMessage(HttpMethod.Post, QueryBatchUri)
            {
                Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(new OsvWire.BatchRequest(queries), OsvJsonContext.Default.BatchRequest)),
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var sent = await BoundedHttp.Send(_client, request, Host, MaxResponseBytes, _timeout, "OSV batch query", cancellationToken).ConfigureAwait(false);
            if (sent.IsFailure)
            {
                return Result.FromError(sent.ErrorDetails);
            }

            OsvWire.BatchResponse? response;
            try
            {
                response = JsonSerializer.Deserialize(sent.Value, OsvJsonContext.Default.BatchResponse);
            }
            catch (JsonException)
            {
                return Result.Unavailable("OSV batch query: the answer is not valid JSON of the expected shape.");
            }

            if (response?.Results is not { } results || results.Count != active.Count)
            {
                return Result.Unavailable("OSV batch query: the answer does not have one result for each query, so it is treated as a partial lookup.");
            }

            List<int> next = [];
            for (var position = 0; position < active.Count; position++)
            {
                var index = active[position];
                if (results[position] is not { } result)
                {
                    return Result.Unavailable("OSV batch query: a result is missing.");
                }

                foreach (var vulnerability in result.Vulns ?? [])
                {
                    if (vulnerability?.Id is not { } id || !IsSafeId(id))
                    {
                        return Result.Unavailable("OSV batch query: a vulnerability identifier is missing or not valid.");
                    }

                    found[index].Add(id);
                }

                if (!string.IsNullOrEmpty(result.NextPageToken))
                {
                    tokens[index] = result.NextPageToken;
                    next.Add(index);
                }
            }

            active = next;
        }

        for (var index = 0; index < chunk.Length; index++)
        {
            var ids = found[index].Distinct(StringComparer.Ordinal).ToArray();
            idsByPair[chunk[index]] = ids;
            _idCache.Set(IdKey(ecosystem, chunk[index]), ids);
        }

        return Result.Ok();
    }

    private async Task<Result<OsvRecord>> GetRecord(string id, CancellationToken cancellationToken)
    {
        if (_recordCache.TryGet(id, out var cached))
        {
            return Result.Ok(cached);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://{Host}/v1/vulns/{Uri.EscapeDataString(id)}"));
        var sent = await BoundedHttp.Send(_client, request, Host, MaxResponseBytes, _timeout, "OSV vulnerability lookup", cancellationToken).ConfigureAwait(false);
        if (sent.IsFailure)
        {
            return Result<OsvRecord>.FromError(sent.ErrorDetails);
        }

        OsvWire.Vulnerability? vulnerability;
        try
        {
            vulnerability = JsonSerializer.Deserialize(sent.Value, OsvJsonContext.Default.Vulnerability);
        }
        catch (JsonException)
        {
            return Result.Unavailable<OsvRecord>("OSV vulnerability lookup: the answer is not valid JSON of the expected shape.");
        }

        if (vulnerability is null || vulnerability.Id != id)
        {
            return Result.Unavailable<OsvRecord>("OSV vulnerability lookup: the answer is not the record that was asked for.");
        }

        Dictionary<string, string[]> versions = new(StringComparer.Ordinal);
        foreach (var affected in vulnerability.Affected ?? [])
        {
            if (affected?.Package is { } package && affected.Versions is { } listed)
            {
                var key = $"{package.Ecosystem}\n{package.Name}";
                versions[key] = versions.TryGetValue(key, out var existing) ? [.. existing, .. listed] : [.. listed];
            }
        }

        var record = new OsvRecord(!string.IsNullOrEmpty(vulnerability.Withdrawn), OsvSeverity.Map(vulnerability), versions);
        _recordCache.Set(id, record);
        return Result.Ok(record);
    }
}
