using System.Globalization;
using System.Text.Json;
using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>
/// Release dates and newest stable versions from the public registries of cargo (crates.io) and npm. apt and Flatpak have
/// no cheap, authoritative source for either, so those managers fail with an <c>Invalid</c> result that says so.
/// The newest stable version is the crates.io <c>max_stable_version</c> or the npm <c>latest</c> dist-tag; the library never
/// compares versions, so it cannot tell whether that version is newer than an installed one, only that it differs.
/// Answers are cached in memory for a limited time; failures are never cached. crates.io asks clients for at most one
/// request a second; callers that look up many crates should space their calls.
/// </summary>
public sealed class RegistryReleaseSource : IReleaseDateSource, ILatestVersionSource
{
    /// <summary>The largest response that is accepted, in bytes.</summary>
    public const int MaxResponseBytes = 32 * 1024 * 1024;

    private const string CratesHost = "crates.io";
    private const string NpmHost = "registry.npmjs.org";

    private readonly HttpClient _client;
    private readonly TimeSpan _timeout;
    private readonly ExpiringCache<string> _latestCache;
    private readonly ExpiringCache<DateTimeOffset> _dateCache;
    private readonly ExpiringCache<RegistryWire.NpmAnswer> _npmCache;

    /// <summary>Creates the source.</summary>
    /// <param name="client">The client; build its handler with <see cref="PackageHttp.CreateHandler"/>. The source does not dispose it.</param>
    /// <param name="timeProvider">The clock for cache expiry.</param>
    /// <param name="cacheLifetime">How long an answer is reused; one hour when <see langword="null"/>.</param>
    /// <param name="requestTimeout">The time allowed for each request; 30 seconds when <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A time span is zero or negative.</exception>
    public RegistryReleaseSource(HttpClient client, TimeProvider timeProvider, TimeSpan? cacheLifetime = null, TimeSpan? requestTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _client = client;
        _timeout = requestTimeout ?? TimeSpan.FromSeconds(30);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_timeout, TimeSpan.Zero, nameof(requestTimeout));
        var lifetime = cacheLifetime ?? TimeSpan.FromHours(1);
        _latestCache = new ExpiringCache<string>(timeProvider, lifetime);
        _dateCache = new ExpiringCache<DateTimeOffset>(timeProvider, lifetime);
        _npmCache = new ExpiringCache<RegistryWire.NpmAnswer>(timeProvider, lifetime);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="version"/> is <see langword="null"/>.</exception>
    public async Task<Result<DateTimeOffset>> GetReleaseDate(PackageManagerKind manager, PackageName name, PackageVersion version, NetworkAccess access, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(version);
        var refusal = Refuse<DateTimeOffset>(manager, access);
        if (refusal is { } refused)
        {
            return refused;
        }

        var key = $"{(int)manager}\n{name.Value}\n{version.Value}";
        if (_dateCache.TryGet(key, out var cached))
        {
            return Result.Ok(cached);
        }

        var date = manager == PackageManagerKind.Cargo
            ? await CrateDate(name, version, cancellationToken).ConfigureAwait(false)
            : await NpmDate(name, version, cancellationToken).ConfigureAwait(false);
        if (date.IsSuccess)
        {
            _dateCache.Set(key, date.Value);
        }

        return date;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public async Task<Result<PackageVersion>> GetLatest(PackageManagerKind manager, PackageName name, NetworkAccess access, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        var refusal = Refuse<PackageVersion>(manager, access);
        if (refusal is { } refused)
        {
            return refused;
        }

        var key = $"{(int)manager}\n{name.Value}";
        if (_latestCache.TryGet(key, out var cached))
        {
            return PackageVersion.Create(cached);
        }

        var latest = manager == PackageManagerKind.Cargo
            ? await CrateLatest(name, cancellationToken).ConfigureAwait(false)
            : await NpmLatest(name, cancellationToken).ConfigureAwait(false);
        if (latest.IsSuccess)
        {
            _latestCache.Set(key, latest.Value.Value);
        }

        return latest;
    }

    private static Result<T>? Refuse<T>(PackageManagerKind manager, NetworkAccess access)
        where T : notnull
    {
        if (access != NetworkAccess.Allowed)
        {
            return Result.Forbidden<T>("The network is not allowed for this call; no registry lookup was made.");
        }

        return manager is PackageManagerKind.Cargo or PackageManagerKind.Npm
            ? null
            : Result.Invalid<T>(
                "Only cargo and npm have a release source; apt and Flatpak publish no cheap release dates.",
                "manager",
                "This package manager has no release source.");
    }

    private static Result<DateTimeOffset> ParseDate(string? text, string what) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? Result.Ok(date)
            : Result.Unavailable<DateTimeOffset>($"{what}: the release time is missing or not a date.");

    private static Result<T> Parse<T>(byte[] body, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info, string what)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize(body, info) is { } value
                ? Result.Ok(value)
                : Result.Unavailable<T>($"{what}: the answer is empty.");
        }
        catch (JsonException)
        {
            return Result.Unavailable<T>($"{what}: the answer is not valid JSON of the expected shape.");
        }
    }

    private async Task<Result<T>> Get<T>(string host, string path, string what, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info, CancellationToken cancellationToken)
        where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://{host}{path}"));
        var sent = await BoundedHttp.Send(_client, request, host, MaxResponseBytes, _timeout, what, cancellationToken).ConfigureAwait(false);
        return sent.IsFailure ? Result<T>.FromError(sent.ErrorDetails) : Parse(sent.Value, info, what);
    }

    private async Task<Result<DateTimeOffset>> CrateDate(PackageName name, PackageVersion version, CancellationToken cancellationToken)
    {
        const string what = "crates.io version lookup";
        var answer = await Get(CratesHost, $"/api/v1/crates/{Uri.EscapeDataString(name.Value)}/{Uri.EscapeDataString(version.Value)}", what, RegistryJsonContext.Default.CrateVersionAnswer, cancellationToken).ConfigureAwait(false);
        return answer.IsFailure ? Result<DateTimeOffset>.FromError(answer.ErrorDetails) : ParseDate(answer.Value.Version?.CreatedAt, what);
    }

    private async Task<Result<PackageVersion>> CrateLatest(PackageName name, CancellationToken cancellationToken)
    {
        const string what = "crates.io crate lookup";
        var answer = await Get(CratesHost, $"/api/v1/crates/{Uri.EscapeDataString(name.Value)}", what, RegistryJsonContext.Default.CrateAnswer, cancellationToken).ConfigureAwait(false);
        if (answer.IsFailure)
        {
            return Result<PackageVersion>.FromError(answer.ErrorDetails);
        }

        return answer.Value.Crate?.MaxStableVersion is { } latest
            ? PackageVersion.Create(latest)
            : Result.NotFound<PackageVersion>($"{what}: the crate has no stable version.");
    }

    private async Task<Result<RegistryWire.NpmAnswer>> Npm(PackageName name, CancellationToken cancellationToken)
    {
        var key = name.Value;
        if (_npmCache.TryGet(key, out var cached))
        {
            return Result.Ok(cached);
        }

        var answer = await Get(NpmHost, $"/{Uri.EscapeDataString(name.Value)}", "npm registry lookup", RegistryJsonContext.Default.NpmAnswer, cancellationToken).ConfigureAwait(false);
        if (answer.IsSuccess)
        {
            _npmCache.Set(key, answer.Value);
        }

        return answer;
    }

    private async Task<Result<DateTimeOffset>> NpmDate(PackageName name, PackageVersion version, CancellationToken cancellationToken)
    {
        var answer = await Npm(name, cancellationToken).ConfigureAwait(false);
        if (answer.IsFailure)
        {
            return Result<DateTimeOffset>.FromError(answer.ErrorDetails);
        }

        return answer.Value.Time is { } time && time.TryGetValue(version.Value, out var text)
            ? ParseDate(text, "npm registry lookup")
            : Result.NotFound<DateTimeOffset>("npm registry lookup: the version has no release time.");
    }

    private async Task<Result<PackageVersion>> NpmLatest(PackageName name, CancellationToken cancellationToken)
    {
        var answer = await Npm(name, cancellationToken).ConfigureAwait(false);
        if (answer.IsFailure)
        {
            return Result<PackageVersion>.FromError(answer.ErrorDetails);
        }

        return answer.Value.DistTags is { } tags && tags.TryGetValue("latest", out var latest)
            ? PackageVersion.Create(latest)
            : Result.NotFound<PackageVersion>("npm registry lookup: the package has no latest tag.");
    }
}
