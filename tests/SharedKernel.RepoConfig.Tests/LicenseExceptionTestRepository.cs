namespace PandaBear.RepoConfig.Tests;

internal static class LicenseExceptionTestRepository
{
    public const string ApprovedVersion = "0.1.0-alpha.94.1";
    public const string ApprovedRevision = "d08008cd3e8901c304377edcc4683de7c3a85ecb";
    public const string ApprovedPackages = "packages=pkg:nuget/StreamJsonRpc@2.25.29,pkg:nuget/Magick.NET-Q8-AnyCPU@15.0.0";
    public const string Configuration = """
        {
          "version": 1,
          "licenseExceptions": {
            "approval": {
              "file": ".config/sharedkernel-candidate.json",
              "values": {
                "packageVersion": "0.1.0-alpha.94.1",
                "libraryRevision": "d08008cd3e8901c304377edcc4683de7c3a85ecb",
                "libraryDirty": false
              }
            },
            "packages": {
              "StreamJsonRpc": "2.25.29",
              "Magick.NET-Q8-AnyCPU": "15.0.0"
            }
          }
        }
        """;

    /// <summary>
    /// Creates a Git repository that stages the configuration, the candidate pin, and a lock file whose net10.0 and
    /// net10.0/win-x64 targets resolve the scenario's packages.
    /// </summary>
    public static async Task<TemporaryGitRepository> Create(LicenseScenario scenario, string configuration = Configuration, bool stageLockFile = true)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = new TemporaryGitRepository();
        try
        {
            await repository.Initialize(cancellationToken);
            repository.WriteText(".config/sharedkernel-repo.json", configuration);
            var revision = scenario.CorrectRevision ? ApprovedRevision : new string('a', 40);
            repository.WriteText(
                ".config/sharedkernel-candidate.json",
                $$"""{"packageVersion":"{{scenario.CandidateVersion}}","libraryRevision":"{{revision}}","libraryDirty":{{(scenario.Dirty ? "true" : "false")}}}""");
            var packageName = string.IsNullOrEmpty(scenario.DependencyVersion) ? "OtherPackage" : scenario.PackageName;
            var magickName = string.IsNullOrEmpty(scenario.MagickVersion) ? "OtherImagePackage" : "Magick.NET-Q8-AnyCPU";
            var targetVersion = scenario.ConflictingTarget ? "2.25.30" : scenario.DependencyVersion;
            repository.WriteText(
                "src/packages.lock.json",
                $$"""
                {
                  "version": 2,
                  "dependencies": {
                    "net10.0": {
                      "{{packageName}}": { "type": "Direct", "resolved": "{{scenario.DependencyVersion}}" },
                      "{{magickName}}": { "type": "Transitive", "resolved": "{{scenario.MagickVersion}}" }
                    },
                    "net10.0/win-x64": {
                      "{{packageName}}": { "type": "Direct", "resolved": "{{targetVersion}}" }
                    }
                  }
                }
                """);
            await repository.Stage(".config/sharedkernel-repo.json", cancellationToken);
            await repository.Stage(".config/sharedkernel-candidate.json", cancellationToken);
            if (stageLockFile)
            {
                await repository.Stage("src/packages.lock.json", cancellationToken);
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
