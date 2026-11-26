namespace SharedKernel.RepoConfig.Tests;

/// <summary>Builds a small repository for testing taxonomy declarations against tracked files.</summary>
internal static class TestTaxonomyTestRepository
{
    private const string ProjectPolicyPath = ".config/test-taxonomy-project.json";

    /// <summary>A suite with an explicit SUT and test type.</summary>
    public const string CanonicalSuite = """
        {
          "path": "tests/Widget.Tests.Unit/Widget.Tests.Unit.csproj",
          "register": "tests/SEAMS.md",
          "seams": [
            {
              "sut": "src/Widget/Widget.csproj",
              "type": "Unit",
              "dependency": "in-process",
              "capabilities": ["example"],
              "lane": "fast"
            }
          ]
        }
        """;

    /// <summary>Stages the optional policy used by the Project-aware check.</summary>
    public static Task StageProjectPolicy(TemporaryGitRepository repository, string policy, CancellationToken ct)
    {
        repository.WriteText(ProjectPolicyPath, policy);
        return repository.Stage(ProjectPolicyPath, ct);
    }

    /// <summary>Adds the supplied repository's issue 27 to the synthetic Project command policy.</summary>
    public static string PolicyWithAdditionalIssue(string policy, string repository) =>
        policy.Replace(
            "\"number\": 27 }]",
            $$"""
            "number": 27 }, { "repository": "{{repository}}", "number": 27 }]
            """,
            StringComparison.Ordinal);

    /// <summary>Creates a repository and stages its production project, test project and seam register.</summary>
    public static async Task<TemporaryGitRepository> Create(
        string suite,
        CancellationToken ct,
        string projectName = "Widget.Tests.Unit")
    {
        var repository = new TemporaryGitRepository();
        try
        {
            await repository.Initialize(ct);
            Dictionary<string, string> files = new(StringComparer.Ordinal)
            {
                [".config/test-taxonomy.json"] = $$"""
                    {
                      "version": 1,
                      "sutRoots": ["src"],
                      "testRoots": ["tests"],
                      "suts": [{"path": "src/Widget/Widget.csproj", "directOwner": "Widget", "requiredTypes": ["Unit"]}],
                      "suites": [{{suite}}],
                      "supportProjects": []
                    }
                    """,
                ["src/Widget/Widget.csproj"] = "<Project />",
                [$"tests/{projectName}/{projectName}.csproj"] = "<Project />",
                ["tests/SEAMS.md"] = "# Test seam register",
            };
            foreach (var (path, content) in files)
            {
                repository.WriteText(path, content);
                await repository.Stage(path, ct);
            }

            return repository;
        }
        catch
        {
            repository.Dispose();
            throw;
        }
    }
}
