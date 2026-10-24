using System.Security.Cryptography;
using System.Text.Json;
using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests;

/// <summary>Guards <c>coverage record-symbols</c> and <c>coverage verify</c>.</summary>
public sealed class CoverageGateTests
{
    [Theory]
    [InlineData(4, 0)]
    [InlineData(5, 0)]
    [InlineData(3, 2)]
    public async Task Changed_code_requires_the_configured_coverage(int coveredLines, int expectedExit)
    {
        // Arrange
        using var scope = new CoverageTestScope(coveredLines);

        // Act
        var result = await scope.RecordAndVerify();

        // Assert
        result.ExitCode.ShouldBe(expectedExit, result.Error);
        result.Output.ShouldContain($"Coverage: {coveredLines}/5 changed executable lines and conditions; required 80%.", StringComparison.Ordinal);
        File.Exists(Path.Combine(scope.Root, "TestResults/coverage-gate.json")).ShouldBeTrue();
    }

    [Fact]
    public async Task Uncovered_conditions_count_towards_the_changed_code_threshold()
    {
        // Arrange
        using var scope = new CoverageTestScope(4, conditions: "50% (1/2)");

        // Act
        var result = await scope.RecordAndVerify();

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Output.ShouldContain("5/7", StringComparison.Ordinal);
        result.Error.ShouldContain("Changed-code coverage 5/7 is below the required 80%.", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("omitted-source")]
    [InlineData("not-cobertura")]
    [InlineData("empty-directory")]
    public async Task Missing_or_invalid_coverage_evidence_fails_closed(string problem)
    {
        // Arrange
        using var scope = new CoverageTestScope(5);
        var report = "coverage.xml";
        switch (problem)
        {
            case "missing":
                File.Delete(Path.Combine(scope.Root, "coverage.xml"));
                break;
            case "malformed":
                scope.WriteFile("coverage.xml", "<coverage>");
                break;
            case "omitted-source":
                scope.WriteFile("coverage.xml", "<coverage><packages /></coverage>");
                break;
            case "not-cobertura":
                scope.WriteFile("coverage.xml", "<report />");
                break;
            default:
                Directory.CreateDirectory(Path.Combine(scope.Root, "reports"));
                report = "reports";
                break;
        }

        // Act
        var result = await scope.RecordAndVerify(report);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldNotBeEmpty();
        using var gate = JsonDocument.Parse(scope.ReadFile("TestResults/coverage-gate.json"));
        gate.RootElement.GetProperty("passed").GetBoolean().ShouldBeFalse();
        gate.RootElement.GetProperty("error").GetString().ShouldNotBeNull().ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("interface-symbols", 0)]
    [InlineData("mixed-symbols", 2)]
    [InlineData("unsupported-hash", 2)]
    [InlineData("absolute-symbols", 0)]
    [InlineData("stale-symbols", 2)]
    [InlineData("executable-symbols", 2)]
    [InlineData("space-path", 2)]
    [InlineData("increment", 0)]
    [InlineData("duplicate", 2)]
    [InlineData("rename", 0)]
    [InlineData("delete", 0)]
    [InlineData("non-executable", 0)]
    [InlineData("excluded", 0)]
    [InlineData("tests-only", 0)]
    [InlineData("malformed-hunk", 2)]
    [InlineData("truncated-hunk", 2)]
    [InlineData("invalid-start", 2)]
    [InlineData("header-without-hunk", 2)]
    [InlineData("quoted-path", 2)]
    [InlineData("not-a-diff", 2)]
    [InlineData("failed-diff", 2)]
    [InlineData("dtd", 2)]
    [InlineData("internal-entity", 2)]
    [InlineData("doctype", 2)]
    public async Task Coverage_evidence_handles_diff_boundaries_without_inflating_coverage(string scenario, int expectedExit)
    {
        // Arrange
        using var scope = new CoverageTestScope(3);
        CoverageScenarios.Apply(scope, scenario);

        // Act
        var result = await scope.RecordAndVerify();

        // Assert
        result.ExitCode.ShouldBe(expectedExit, result.Error);
        File.Exists(Path.Combine(scope.Root, "TestResults/coverage-gate.json")).ShouldBeTrue();
    }

    [Fact]
    public async Task Per_project_reports_in_a_directory_merge_into_one_verdict()
    {
        // Arrange
        using var scope = new CoverageTestScope(0);
        File.Delete(Path.Combine(scope.Root, "coverage.xml"));
        scope.WriteFile("results/A.Tests/net10.0_linux-x64/coverage.cobertura.xml", CoverageTestScope.Report("src/Core.cs", [(1, true, null), (2, true, null), (5, false, null)]));
        scope.WriteFile("results/B.Tests/net10.0_linux-x64/coverage.cobertura.xml", CoverageTestScope.Report("src/Core.cs", [(3, true, null), (4, true, null), (5, false, null)]));
        scope.WriteFile("results/B.Tests/net10.0_linux-x64/unrelated.xml", "not a report");

        // Act
        var result = await scope.RecordAndVerify("results");

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain("Coverage: 4/5 ", StringComparison.Ordinal);
        using var gate = JsonDocument.Parse(scope.ReadFile("TestResults/coverage-gate.json"));
        gate.RootElement.GetProperty("reports").EnumerateArray().Select(static report => report.GetString()).ShouldBe(
        [
            "results/A.Tests/net10.0_linux-x64/coverage.cobertura.xml",
            "results/B.Tests/net10.0_linux-x64/coverage.cobertura.xml",
        ]);
    }

    [Fact]
    public async Task Gate_evidence_records_the_configuration_hash_the_base_and_both_merge_parents()
    {
        // Arrange
        using var scope = new CoverageTestScope(5);
        var configuration = await File.ReadAllBytesAsync(Path.Combine(scope.Root, ".config/sharedkernel-ci.json"), TestContext.Current.CancellationToken);

        // Act
        var result = await scope.RecordAndVerify();

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        using var gate = JsonDocument.Parse(scope.ReadFile("TestResults/coverage-gate.json"));
        var root = gate.RootElement;
        root.GetProperty("passed").GetBoolean().ShouldBeTrue();
        root.GetProperty("thresholdPercent").GetInt32().ShouldBe(80);
        root.GetProperty("covered").GetInt64().ShouldBe(5);
        root.GetProperty("total").GetInt64().ShouldBe(5);
        root.GetProperty("revision").GetString().ShouldBe(CoverageTestScope.Revision);
        root.GetProperty("parents").EnumerateArray().Select(static parent => parent.GetString()).ShouldBe(CoverageTestScope.Parents.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        root.GetProperty("base").GetString().ShouldBe(CoverageTestScope.BaseRevision);
        root.GetProperty("configurationSha256").GetString().ShouldBe(Convert.ToHexStringLower(SHA256.HashData(configuration)));
        root.TryGetProperty("error", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task The_diff_runs_with_options_that_override_repository_git_configuration()
    {
        // Arrange
        using var scope = new CoverageTestScope(5);
        var processes = scope.Processes;
        (await RepositoryToolInvocation.Run(scope.Root, ["coverage", "record-symbols"], processes.Run, TestContext.Current.CancellationToken)).ExitCode.ShouldBe(0);

        // Act
        var result = await scope.Verify(processes);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        processes.Commands.ShouldBe(
        [
            "git rev-parse HEAD",
            "git rev-parse HEAD",
            "git rev-parse HEAD^@",
            "git -c core.quotePath=false diff --no-ext-diff --no-textconv --no-color --src-prefix=a/ --dst-prefix=b/ --unified=0 --inter-hunk-context=0 --text --find-renames "
                + CoverageTestScope.BaseRevision + "...HEAD --",
        ]);
    }

    [Fact]
    public async Task Symbol_evidence_for_another_revision_fails_before_the_diff()
    {
        // Arrange
        using var scope = new CoverageTestScope(5);
        (await RepositoryToolInvocation.Run(scope.Root, ["coverage", "record-symbols"], scope.Processes.Run, TestContext.Current.CancellationToken)).ExitCode.ShouldBe(0);
        var evidence = Path.Combine(scope.Root, "TestResults/symbol-evidence.json");
        await File.WriteAllTextAsync(
            evidence,
            (await File.ReadAllTextAsync(evidence, TestContext.Current.CancellationToken)).Replace(CoverageTestScope.Revision, CoverageTestScope.BaseRevision, StringComparison.Ordinal),
            TestContext.Current.CancellationToken);
        var processes = scope.Processes;

        // Act
        var result = await scope.Verify(processes);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("records revision " + CoverageTestScope.BaseRevision + ", but HEAD is " + CoverageTestScope.Revision, StringComparison.Ordinal);
        processes.Commands.ShouldNotContain(static command => command.Contains(" diff ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Symbol_evidence_is_bound_to_the_file_hashes()
    {
        // Arrange
        using var scope = new CoverageTestScope(5);
        (await RepositoryToolInvocation.Run(scope.Root, ["coverage", "record-symbols"], scope.Processes.Run, TestContext.Current.CancellationToken)).ExitCode.ShouldBe(0);
        scope.WriteFile("src/Placeholder/bin/Release/net10.0/Placeholder.pdb", "changed symbols");

        // Act
        var result = await scope.Verify(scope.Processes);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("The symbol file src/Placeholder/bin/Release/net10.0/Placeholder.pdb does not match its evidence.", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("src/../../outside/bin/Release/outside.pdb")]
    [InlineData("/etc/bin/Release/outside.pdb")]
    [InlineData("tests/bin/Release/Tests.pdb")]
    [InlineData("src/bin/Debug/Core.pdb")]
    public async Task Symbol_evidence_cannot_name_files_outside_the_symbol_directories(string path)
    {
        // Arrange
        using var scope = new CoverageTestScope(5);
        scope.WriteFile(
            "TestResults/symbol-evidence.json",
            JsonSerializer.Serialize(new Dictionary<string, object> { ["revision"] = CoverageTestScope.Revision, ["files"] = new Dictionary<string, string> { [path] = "00" } }));

        // Act
        var result = await scope.Verify(scope.Processes);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("which is not a Release .pdb under a symbol directory of the checkout.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Recording_symbols_requires_a_release_build()
    {
        // Arrange
        using var scope = new CoverageTestScope(5);
        Directory.Delete(Path.Combine(scope.Root, "src/Placeholder"), recursive: true);

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["coverage", "record-symbols"], scope.Processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("No Release portable PDB exists under src", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Coverage_commands_require_the_coverage_policy()
    {
        // Arrange
        using var scope = new CoverageTestScope(5);
        scope.WriteFile(".config/sharedkernel-ci.json", RepositoryTestScope.DefaultConfiguration);

        // Act
        var record = await RepositoryToolInvocation.Run(scope.Root, ["coverage", "record-symbols"], scope.Processes.Run, TestContext.Current.CancellationToken);
        var verify = await scope.Verify(scope.Processes);

        // Assert
        record.ExitCode.ShouldBe(2);
        record.Error.ShouldBe(".config/sharedkernel-ci.json: $.coverage is required by the coverage commands." + Environment.NewLine);
        verify.ExitCode.ShouldBe(2);
        verify.Error.ShouldBe(record.Error);
    }

    [Theory]
    [InlineData("HEAD~1")]
    [InlineData("0123456789abcdef0123456789abcdef0123456g")]
    [InlineData("0123456789abcdef")]
    public async Task The_base_must_be_a_full_object_id(string baseRevision)
    {
        // Arrange
        using var scope = new CoverageTestScope(5);
        var processes = scope.Processes;

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root,
            ["coverage", "verify", "--report", "coverage.xml", "--base", baseRevision, "--symbols", "TestResults/symbol-evidence.json"],
            processes.Run,
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe("--base requires a full hexadecimal Git object ID." + Environment.NewLine);
        processes.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_merge_checkout_counts_only_the_changes_that_the_merge_brings_to_its_first_parent()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new CoverageTestScope(0);
        scope.WriteFile("src/Other.cs", "a\n");
        scope.WriteFile("src/Core.cs", "one\n");
        File.Delete(Path.Combine(scope.Root, "coverage.xml"));
        await TestGit.CommitAll(scope.Root, ct);
        await TestGit.Run(scope.Root, ["switch", "--quiet", "--create", "feature"], ct);
        scope.WriteFile("src/Core.cs", "one\ntwo\nthree\n");
        var feature = await TestGit.Commit(scope.Root, ct);
        await TestGit.Run(scope.Root, ["switch", "--quiet", "-"], ct);
        scope.WriteFile("src/Other.cs", "a\nb\nc\nd\n");
        var target = await TestGit.Commit(scope.Root, ct);
        await TestGit.Run(scope.Root, ["-c", "user.name=Stage Tests", "-c", "user.email=stage-tests@example.invalid", "merge", "--quiet", "--no-ff", "--no-edit", "feature"], ct);
        scope.WriteFile("results/coverage.cobertura.xml", "<coverage><packages><package><classes>"
            + CoverageScenarios.Class("src/Core.cs", (1, true), (2, true), (3, true))
            + CoverageScenarios.Class("src/Other.cs", (1, true), (2, false), (3, false), (4, false))
            + "</classes></package></packages></coverage>");

        // Act
        var record = await RepositoryToolInvocation.Run(scope.Root, ["coverage", "record-symbols"], ProcessRunner.Run, ct);
        var result = await RepositoryToolInvocation.Run(
            scope.Root, ["coverage", "verify", "--report", "results", "--base", target, "--symbols", "TestResults/symbol-evidence.json"], ProcessRunner.Run, ct);

        // Assert
        record.ExitCode.ShouldBe(0, record.Error);
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain("Coverage: 2/2 ", StringComparison.Ordinal);
        using var gate = JsonDocument.Parse(scope.ReadFile("TestResults/coverage-gate.json"));
        gate.RootElement.GetProperty("parents").EnumerateArray().Select(static parent => parent.GetString()).ShouldBe([target, feature]);
        gate.RootElement.GetProperty("revision").GetString().ShouldBe((await TestGit.Run(scope.Root, ["rev-parse", "HEAD"], ct)).Trim());
    }

    [Fact]
    public async Task Repository_git_configuration_and_attributes_cannot_hide_or_move_changed_lines()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new CoverageTestScope(0);
        File.Delete(Path.Combine(scope.Root, "src/Core.cs"));
        scope.WriteFile("src/Converted.cs", "x\n");
        scope.WriteFile("src/Binary.cs", "x\n");
        var baseRevision = await TestGit.CommitAll(scope.Root, ct);
        scope.WriteFile("src/Converted.cs", "x\ny\nz\n");
        scope.WriteFile("src/Binary.cs", "x\n\ny\n");
        scope.WriteFile("src/Ação.cs", "new\n");
        scope.WriteFile(".gitattributes", "src/Converted.cs diff=hostile\nsrc/Binary.cs -diff\n");
        await TestGit.Commit(scope.Root, ct);
        foreach (var (name, value) in new[]
        {
            ("diff.external", "true"),
            ("diff.hostile.command", "true"),
            ("diff.hostile.textconv", "true"),
            ("diff.noprefix", "true"),
            ("diff.mnemonicPrefix", "true"),
            ("diff.interHunkContext", "10"),
            ("diff.suppressBlankEmpty", "true"),
            ("diff.renames", "copies"),
            ("core.quotePath", "true"),
            ("color.ui", "always"),
        })
        {
            await TestGit.Run(scope.Root, ["config", name, value], ct);
        }

        scope.WriteFile("results/coverage.cobertura.xml", "<coverage><packages><package><classes>"
            + CoverageScenarios.Class("src/Converted.cs", (1, true), (2, false), (3, false))
            + CoverageScenarios.Class("src/Binary.cs", (1, true), (2, false), (3, false))
            + CoverageScenarios.Class("src/Ação.cs", (1, false))
            + "</classes></package></packages></coverage>");

        // Act
        var record = await RepositoryToolInvocation.Run(scope.Root, ["coverage", "record-symbols"], ProcessRunner.Run, ct);
        var result = await RepositoryToolInvocation.Run(
            scope.Root, ["coverage", "verify", "--report", "results", "--base", baseRevision, "--symbols", "TestResults/symbol-evidence.json"], ProcessRunner.Run, ct);

        // Assert
        record.ExitCode.ShouldBe(0, record.Error);
        result.Output.ShouldContain("Coverage: 0/5 ", StringComparison.Ordinal);
        result.ExitCode.ShouldBe(2);
    }
}
