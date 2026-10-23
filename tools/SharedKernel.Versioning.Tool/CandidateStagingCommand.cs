using System.Security.Cryptography;

namespace PandaBear.Versioning.Tool;

internal static class CandidateStagingCommand
{
    public static async Task Run(string root, string candidate, string feed, string destination, TextWriter output, CancellationToken ct)
    {
        var evidence = CandidateManifest.ReadAndVerify(candidate, feed);
        var revision = (await CommandRunner.Run("git", ["rev-parse", "HEAD"], root, ct).ConfigureAwait(false)).Trim();
        if (evidence["libraryRevision"]?.GetValue<string>() != revision || evidence["libraryDirty"]?.GetValue<bool>() != false)
        {
            throw new ArgumentException("Candidate must identify the committed library revision used for bootstrap.");
        }

        await Stage(candidate, feed, destination, ct).ConfigureAwait(false);
        await output.WriteLineAsync("Verified candidate staged: " + destination).ConfigureAwait(false);
    }

    internal static async Task Stage(string candidate, string feed, string destination, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var evidence = CandidateManifest.ReadAndVerify(candidate, feed);
        var hashes = evidence["packageSha256"]!.AsObject();
        // Check every existing destination before writing any artifacts.
        foreach (var (name, value) in hashes)
        {
            var existing = Path.Combine(destination, name);
            if (File.Exists(existing))
            {
                using var stream = File.OpenRead(existing);
                if (Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false)) != value!.GetValue<string>())
                {
                    throw new ArgumentException("Refusing to overwrite a different candidate package: " + name);
                }
            }
        }

        Directory.CreateDirectory(destination);
        foreach (var (name, value) in hashes)
        {
            ct.ThrowIfCancellationRequested();
            var target = Path.Combine(destination, name);
            if (File.Exists(target))
            {
                continue;
            }
            var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var source = File.OpenRead(Path.Combine(feed, name)))
                using (var copy = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, useAsync: true))
                {
                    await source.CopyToAsync(copy, ct).ConfigureAwait(false);
                    copy.Position = 0;
                    if (Convert.ToHexStringLower(await SHA256.HashDataAsync(copy, ct).ConfigureAwait(false)) != value!.GetValue<string>())
                    {
                        throw new ArgumentException("Candidate package changed during staging: " + name);
                    }
                }

                ct.ThrowIfCancellationRequested();
                File.Move(temporary, target, overwrite: false);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        CandidateManifest.Write(Path.Combine(destination, "candidate.json"), evidence);
    }
}
