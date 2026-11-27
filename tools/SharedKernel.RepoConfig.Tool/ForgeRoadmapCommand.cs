using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using SharedKernel.Forgejo;
using SharedKernel.Repositories;
using SharedKernel.Repositories.Forgejo;
using SharedKernel.Repositories.GitHub;

namespace SharedKernel.RepoConfig.Tool;

/// <summary>Wires the read-only forge roadmap providers to the existing query and verification commands.</summary>
internal static class ForgeRoadmapCommand
{
    /// <summary>Runs verification, or renders the named query when one was supplied.</summary>
    /// <exception cref="InvalidOperationException">The repository or query arguments are unsupported.</exception>
    public static async Task<int> Run(
        string rootPath, string repository, Uri? forgejoServer, RepoConfigToolEnvironment environment,
        TextWriter output, TextWriter error, CancellationToken cancellationToken,
        string? query = null, string? value = null, string? type = null, int limit = 0)
    {
        if (query is not (null or "next-priority" or "next-unblocked" or "next-work" or "blockers-of"))
        {
            throw new InvalidOperationException("Forge roadmap queries support next-priority, next-unblocked, next-work, and blockers-of.");
        }

        if (type is not null)
        {
            throw new InvalidOperationException("--type requires the file-backed roadmap.");
        }

        var snapshot = await WithReader(forgejoServer, environment,
            reader => ForgeRoadmapReader.Load(rootPath, repository, reader, query == "blockers-of" ? value : null, cancellationToken)).ConfigureAwait(false);
        if (snapshot.Issues.Count > 0)
        {
            RepoConfigToolApplication.WriteIssues(error, "Forge roadmap verification failed:", snapshot.Issues);
            return 1;
        }

        if (query is null)
        {
            await output.WriteLineAsync("Forge roadmap priorities and references are valid.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 0;
        }

        IEnumerable<RoadmapItemSnapshot> items = query switch
        {
            "blockers-of" => snapshot.BlockersOf(value!).OrderByPriority(),
            "next-work" => snapshot.NextWork(),
            _ => snapshot.OpenItems(unblockedOnly: query == "next-unblocked"),
        };
        RepoConfigToolApplication.WriteItems(output, items.Take(limit));
        return 0;
    }

    /// <summary>Prints candidate priorities and reports discrepancies without modifying repository or forge data.</summary>
    public static async Task<int> PreviewMigration(
        string rootPath, string repository, Uri? server, RepoConfigToolEnvironment environment,
        TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var (prioritiesJson, issues) = await WithReader(server, environment,
            reader => ForgeRoadmapMigration.Preview(rootPath, repository, reader, cancellationToken)).ConfigureAwait(false);
        if (prioritiesJson.Length > 0)
        {
            await output.WriteLineAsync(prioritiesJson.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        if (issues.Count > 0)
        {
            RepoConfigToolApplication.WriteIssues(error, "Forge roadmap migration differences:", issues);
            return 1;
        }

        return 0;
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The reader owns the HTTP handler.")]
    private static async Task<T> WithReader<T>(
        Uri? server, RepoConfigToolEnvironment environment, Func<IForgeIssueReader, Task<T>> action)
    {
        if (server is null)
        {
            using var connection = GitHubConnections.FromEnvironment(environment.HttpClient, "roadmap issue reads");
            return await action(new GitHubForgeIssueReader(connection)).ConfigureAwait(false);
        }

        var handler = environment.ForgejoHandler ?? new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true };
        var token = ForgejoToken.Find(Environment.GetEnvironmentVariable);
        ForgejoIssueReader reader;
        try
        {
            reader = new ForgejoIssueReader(handler, server, new ProductInfoHeaderValue("sharedkernel-repo", "1.0"), token);
        }
        catch
        {
            handler.Dispose();
            throw;
        }

        using (reader)
        {
            return await action(reader).ConfigureAwait(false);
        }
    }
}
