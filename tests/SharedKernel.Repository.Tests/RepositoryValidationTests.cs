using PandaBear.Repository.Tool;

namespace PandaBear.Repository.Tests;

public sealed class RepositoryValidationTests
{
    [Fact]
    public void Selected_projects_restore_and_build_as_one_graph()
    {
        using var scope = new RepositoryTestScope();
        var selection = Path.Combine(scope.Root, "TestResults", "selection");
        Directory.CreateDirectory(selection);
        File.WriteAllLines(Path.Combine(selection, "build-projects.txt"), ["src/Core/Core.csproj", "src/Dependent/Dependent.csproj", "tests/Core.Tests/Core.Tests.csproj"]);
        File.WriteAllLines(Path.Combine(selection, "test-projects.txt"), ["tests/Core.Tests/Core.Tests.csproj"]);
        File.WriteAllText(Path.Combine(selection, "revision.txt"), "fixture");
        var commands = new List<string>();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var result = RepositoryToolApplication.Run(["validate", "--root", scope.Root], output, error,
            (executable, arguments, directory) =>
            {
                if (executable == "git") { return "fixture"; }
                commands.Add(string.Join(' ', arguments));
                return string.Empty;
            });

        result.ShouldBe(0);
        commands.Count(command => command.StartsWith("restore ", StringComparison.Ordinal)).ShouldBe(1);
        commands.Count(command => command.StartsWith("build ", StringComparison.Ordinal)).ShouldBe(1);
        commands.Count(command => command.StartsWith("test --solution ", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public void Stale_selection_cannot_execute_validation()
    {
        using var scope = new RepositoryTestScope();
        var selection = Path.Combine(scope.Root, "TestResults", "selection");
        Directory.CreateDirectory(selection);
        File.WriteAllText(Path.Combine(selection, "revision.txt"), "previous");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var executed = false;

        var result = RepositoryToolApplication.Run(["validate", "--root", scope.Root], output, error,
            (executable, arguments, directory) =>
            {
                if (executable == "git") { return "current"; }
                executed = true;
                return string.Empty;
            });

        result.ShouldBe(2);
        executed.ShouldBeFalse();
        error.ToString().ShouldContain("revision", StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void A_failed_graph_build_does_not_run_tests_or_claim_success()
    {
        using var scope = new RepositoryTestScope();
        var selection = Path.Combine(scope.Root, "TestResults", "selection");
        Directory.CreateDirectory(selection);
        File.WriteAllLines(Path.Combine(selection, "build-projects.txt"), ["tests/Core.Tests/Core.Tests.csproj"]);
        File.WriteAllLines(Path.Combine(selection, "test-projects.txt"), ["tests/Core.Tests/Core.Tests.csproj"]);
        File.WriteAllText(Path.Combine(selection, "revision.txt"), "fixture");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var ranTests = false;

        var result = RepositoryToolApplication.Run(["validate", "--root", scope.Root], output, error,
            (executable, arguments, directory) =>
            {
                if (executable == "git") { return "fixture"; }
                if (arguments[0] == "build") { throw new InvalidOperationException("Build failed."); }
                if (arguments[0] == "test") { ranTests = true; }
                return string.Empty;
            });

        result.ShouldBe(2);
        ranTests.ShouldBeFalse();
        File.ReadAllText(Path.Combine(selection, "execution-all", "timing.tsv")).ShouldContain("failed", StringComparison.Ordinal);
    }

    [Fact]
    public void Ordinary_group_excludes_hosted_tests_without_capping_test_parallelism()
    {
        using var scope = new RepositoryTestScope();
        scope.AddProject("tests/Hosted/Hosted.csproj", """<Project Sdk="Aspire.AppHost.Sdk" />""");
        var selection = Path.Combine(scope.Root, "TestResults", "selection");
        Directory.CreateDirectory(selection);
        File.WriteAllLines(Path.Combine(selection, "build-projects.txt"), ["tests/Core.Tests/Core.Tests.csproj", "tests/Hosted/Hosted.csproj"]);
        File.WriteAllLines(Path.Combine(selection, "test-projects.txt"), ["tests/Core.Tests/Core.Tests.csproj", "tests/Hosted/Hosted.csproj"]);
        File.WriteAllText(Path.Combine(selection, "revision.txt"), "fixture");
        var commands = new List<string>();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var result = RepositoryToolApplication.Run(["validate", "--root", scope.Root, "--test-group", "ordinary"], output, error,
            (executable, arguments, directory) =>
            {
                if (executable == "git") { return "fixture"; }
                commands.Add(string.Join(' ', arguments));
                return string.Empty;
            });

        result.ShouldBe(0);
        File.ReadAllText(Path.Combine(selection, "execution-ordinary", "build.slnx")).ShouldNotContain("Hosted", StringComparison.Ordinal);
        commands.Any(command => command.Contains("--max-parallel-test-modules", StringComparison.Ordinal)).ShouldBeFalse();
    }
}
