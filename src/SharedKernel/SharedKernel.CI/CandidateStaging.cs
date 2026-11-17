using PandaBear.Versioning;

namespace PandaBear.CI;

/// <summary>
/// Verifies a pinned candidate against a clean library checkout and stages its packages, from a directory or a forge
/// release, in a local feed.
/// </summary>
internal static class CandidateStaging
{
    private const string ManifestName = "candidate.json";

    /// <summary>
    /// Validates the pin, checks the SDK lockstep and the library checkout, then stages every missing package and writes
    /// the pin as <c>candidate.json</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The feed directory does not exist.</exception>
    /// <exception cref="InvalidDataException">A staged package does not match its pinned SHA-256 hash.</exception>
    /// <remarks>
    /// Packages are copied or downloaded into a staging directory beside the output, verified there, and only then moved
    /// into the output, so the output never holds an unverified package. An identical package already in the output is
    /// kept; a different one is refused and never overwritten. The pin is written last and atomically, and other
    /// versions in the output are kept.
    /// </remarks>
    public static async Task Run(CandidateStagingRequest request, IReleaseAssets? release, CandidateStagingServices services, TextWriter output, CancellationToken ct)
    {
        var pinBytes = await BoundedFile.Read(request.Pin, CandidatePin.MaxBytes, "The pin " + request.Pin, ct).ConfigureAwait(false);
        var pin = CandidatePin.Parse(pinBytes, "The pin " + request.Pin);
        if (request.Feed is not null && !Directory.Exists(request.Feed))
        {
            throw new InvalidOperationException($"The feed directory does not exist: {request.Feed}.");
        }

        await SdkLockstep.Verify(request.Root, request.LibraryRoot, ct).ConfigureAwait(false);
        await LibraryCheckout.Verify(services.Git, request.LibraryRoot, pin.LibraryRevision, ct).ConfigureAwait(false);
        StagingScratch.SweepStale(request.Output, services.TimeProvider);

        using var http = release is null ? null : CreateHttpClient(services.CreateHttpHandler(), services);
        var assets = http is null ? null : new ReleaseAssetClient(http, services.TimeProvider, services.Limits);
        var tag = "v" + pin.PackageVersion;
        if (assets is not null)
        {
            await VerifyRelease(assets, release!, tag, pin, ct).ConfigureAwait(false);
        }

        var missing = await FindMissing(pin, request.Output, ct).ConfigureAwait(false);
        using (var scratch = StagingScratch.Create(request.Output))
        {
            long downloaded = 0;
            foreach (var name in missing)
            {
                var staged = Path.Combine(scratch.FullPath, name);
                if (assets is null)
                {
                    File.Copy(Path.Combine(request.Feed!, name), staged);
                }
                else
                {
                    var uri = await release!.Locate(tag, name, ct).ConfigureAwait(false);
                    downloaded += await Download(assets, services.Limits, uri, staged, downloaded, ct).ConfigureAwait(false);
                }

                if (!string.Equals(await CandidateManifest.HashPackage(staged, ct).ConfigureAwait(false), pin.PackageSha256[name], StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"{name} does not match its pinned SHA-256 hash.");
                }
            }

            // Once every package is verified, the moves and the pin write are not interrupted, so a cancellation never
            // leaves the pin beside packages that are not all in place.
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(request.Output);
            foreach (var name in missing)
            {
                await MoveIntoOutput(Path.Combine(scratch.FullPath, name), Path.Combine(request.Output, name), pin.PackageSha256[name]).ConfigureAwait(false);
            }

            var manifest = Path.Combine(scratch.FullPath, ManifestName);
            await File.WriteAllBytesAsync(manifest, pinBytes, CancellationToken.None).ConfigureAwait(false);
            File.Move(manifest, Path.Combine(request.Output, ManifestName), overwrite: true);
        }

        var present = pin.PackageSha256.Count - missing.Count;
        await output.WriteLineAsync(
            ($"Staged {pin.PackageVersion} in {request.Output}: {Packages(missing.Count)} added, {present} already present.").AsMemory(),
            CancellationToken.None).ConfigureAwait(false);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler, CandidateStagingServices services)
    {
        var client = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.Add(services.UserAgent);
        return client;
    }

    // The release's candidate.json is fetched even when every package is cached, so the packages of a deleted or
    // re-created release are refused. Evidence properties may differ from the pin; identity properties may not.
    private static async Task VerifyRelease(ReleaseAssetClient assets, IReleaseAssets release, string tag, CandidatePin pin, CancellationToken ct)
    {
        var uri = await release.Locate(tag, ManifestName, ct).ConfigureAwait(false);
        using var manifest = new MemoryStream();
        await assets.Copy(uri, manifest, CandidatePin.MaxBytes, "the manifest limit of " + DataSize.Format(CandidatePin.MaxBytes), ct).ConfigureAwait(false);
        var released = CandidatePin.Parse(manifest.ToArray(), "The release manifest " + uri);
        var differences = pin.IdentityDifferences(released);
        if (differences.Length > 0)
        {
            throw new InvalidDataException($"The release manifest {uri} does not match the pin: {string.Join(", ", differences)} differs.");
        }
    }

    private static async Task<List<string>> FindMissing(CandidatePin pin, string output, CancellationToken ct)
    {
        List<string> missing = [];
        foreach (var (name, hash) in pin.PackageSha256.OrderBy(static package => package.Key, StringComparer.Ordinal))
        {
            var target = Path.Combine(output, name);
            if (!File.Exists(target))
            {
                missing.Add(name);
            }
            else if (!string.Equals(await CandidateManifest.HashPackage(target, ct).ConfigureAwait(false), hash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Refusing to overwrite {target}: it differs from the pinned package.");
            }
        }

        return missing;
    }

    private static async Task<long> Download(
        ReleaseAssetClient assets, CandidateDownloadLimits limits, Uri uri, string destination, long downloaded, CancellationToken ct)
    {
        var remaining = limits.MaxTotalBytes - downloaded;
        var (maxBytes, limit) = remaining < limits.MaxPackageBytes
            ? (remaining, "the total download limit of " + DataSize.Format(limits.MaxTotalBytes))
            : (limits.MaxPackageBytes, "the per-package limit of " + DataSize.Format(limits.MaxPackageBytes));
        var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920, useAsync: true);
        await using (file.ConfigureAwait(false))
        {
            return await assets.Copy(uri, file, maxBytes, limit, ct).ConfigureAwait(false);
        }
    }

    // File.Move without overwrite fails when the target exists, so a package in the output is never overwritten. A
    // concurrent stager of the same pin may have moved the identical package first, which is fine.
    private static async Task MoveIntoOutput(string staged, string target, string hash)
    {
        try
        {
            File.Move(staged, target, overwrite: false);
        }
        catch (IOException) when (File.Exists(target))
        {
            if (!string.Equals(await CandidateManifest.HashPackage(target, CancellationToken.None).ConfigureAwait(false), hash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Refusing to overwrite {target}: it differs from the pinned package.");
            }
        }
    }

    private static string Packages(int count) => count == 1 ? "1 package" : count + " packages";
}
