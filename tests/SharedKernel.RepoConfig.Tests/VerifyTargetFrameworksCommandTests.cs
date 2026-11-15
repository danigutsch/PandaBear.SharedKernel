namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// Tests the verify-target-frameworks command against seeded Git repositories of project files.
/// </summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class VerifyTargetFrameworksCommandTests
{
    private const string Heading = TargetFrameworkTestRepository.Heading;
    private const string Multi = "<TargetFrameworks>net10.0;net11.0</TargetFrameworks>";

    [Fact]
    public async Task Verify_target_frameworks_accepts_a_policy_conforming_repository()
    {
        // Arrange
        using var repository = await TargetFrameworkTestRepository.Create(TargetFrameworkTestRepository.Files(
            ("Directory.Build.props", TargetFrameworkTestRepository.Props("""
                <PropertyGroup>
                  <RepoFrameworks Condition="'$(RepoFrameworks)' == ''">net10.0;net11.0</RepoFrameworks>
                  <TargetFrameworks>$(RepoFrameworks)</TargetFrameworks>
                </PropertyGroup>
                """)),
            ("src/Directory.Build.props", TargetFrameworkTestRepository.Props("""
                <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
                <PropertyGroup>
                  <TargetFramework>net10.0</TargetFramework>
                </PropertyGroup>
                """)),
            ("tools/Directory.Build.targets", TargetFrameworkTestRepository.Props("<PropertyGroup><RollForward>Major</RollForward></PropertyGroup>")),
            ("src/Lib/Lib.csproj", TargetFrameworkTestRepository.Project("<TargetFramework></TargetFramework>\n    " + Multi)),
            ("src/Lib/packages.lock.json", TargetFrameworkTestRepository.Lock("net10.0", "net11.0/linux-x64")),
            ("app/App.csproj", TargetFrameworkTestRepository.Project(string.Empty)),
            ("tools/T/T.csproj", TargetFrameworkTestRepository.Project("<TargetFrameworks></TargetFrameworks><TargetFramework>net10.0</TargetFramework>")),
            ("global.json", """{"sdk":{"version":"11.0.100"}}"""),
            ("run.sh", "dotnet run --project src/Lib --framework net10.0\ndotnet run --project tools/T/T.csproj\n")));

        // Act
        var result = await TargetFrameworkTestRepository.Run(repository);

        // Assert
        result.StandardError.ShouldBe(string.Empty);
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe("Target frameworks match the configured policy." + Environment.NewLine);
    }

    [Fact]
    public async Task Verify_target_frameworks_reports_a_project_that_no_rule_includes()
    {
        // Arrange
        using var repository = await TargetFrameworkTestRepository.Create(
            TargetFrameworkTestRepository.Files(("app/App.csproj", TargetFrameworkTestRepository.Project("<TargetFramework>net10.0</TargetFramework>"))),
            TargetFrameworkTestRepository.Configuration("""[{ "include": "tools/*.csproj", "frameworks": ["net10.0"] }]"""));

        // Act
        var result = await TargetFrameworkTestRepository.Run(repository);

        // Assert
        result.ExitCode.ShouldBe(1);
        TargetFrameworkTestRepository.ErrorLines(result).ShouldBe(
            [Heading, "- app/App.csproj: No targetFrameworks.projects rule includes this project."]);
    }

    [Fact]
    public async Task Verify_target_frameworks_reports_a_framework_that_overrides_the_framework_list()
    {
        // Arrange
        using var repository = await TargetFrameworkTestRepository.Create(TargetFrameworkTestRepository.Files(
            ("Directory.Build.props", TargetFrameworkTestRepository.Props("<PropertyGroup>" + Multi + "</PropertyGroup>")),
            ("src/Directory.Build.props", TargetFrameworkTestRepository.Props("""
                <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
                <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                """)),
            ("src/A/A.csproj", TargetFrameworkTestRepository.Project(string.Empty))));

        // Act
        var result = await TargetFrameworkTestRepository.Run(repository);

        // Assert
        result.ExitCode.ShouldBe(1);
        TargetFrameworkTestRepository.ErrorLines(result).ShouldBe(
        [
            Heading,
            "- src/A/A.csproj: TargetFramework 'net10.0' from src/Directory.Build.props overrides TargetFrameworks 'net10.0;net11.0', so the project builds only net10.0; clear it with <TargetFramework></TargetFramework> where TargetFrameworks is set.",
        ]);
    }

    [Fact]
    public async Task Verify_target_frameworks_reports_frameworks_that_differ_from_the_rule()
    {
        // Arrange
        using var repository = await TargetFrameworkTestRepository.Create(TargetFrameworkTestRepository.Files(
            ("a/A.csproj", TargetFrameworkTestRepository.Project("<TargetFramework>net10.0</TargetFramework>")),
            ("b/B.csproj", TargetFrameworkTestRepository.Project(string.Empty)),
            ("tools/T/T.csproj", TargetFrameworkTestRepository.Project("<TargetFramework>net11.0</TargetFramework>"))));

        // Act
        var result = await TargetFrameworkTestRepository.Run(repository);

        // Assert
        result.ExitCode.ShouldBe(1);
        TargetFrameworkTestRepository.ErrorLines(result).ShouldBe(
        [
            Heading,
            "- a/A.csproj: Declares net10.0, but the rule '*.csproj' expects net10.0;net11.0.",
            "- b/B.csproj: Declares no target framework.",
            "- tools/T/T.csproj: Declares net11.0, but the rule 'tools/*.csproj' expects net10.0.",
            "- tools/T/T.csproj: RollForward is 'Minor (default)', but the rule 'tools/*.csproj' expects 'Major'.",
        ],
        "The first matching rule applies, so the tools rule wins over the catch-all rule.");
    }

    [Fact]
    public async Task Verify_target_frameworks_reads_roll_forward_from_the_project_and_the_targets_chain()
    {
        // Arrange
        using var repository = await TargetFrameworkTestRepository.Create(TargetFrameworkTestRepository.Files(
            ("tools/A/Directory.Build.targets", TargetFrameworkTestRepository.Props("<PropertyGroup><RollForward>LatestMajor</RollForward></PropertyGroup>")),
            ("tools/A/A.csproj", TargetFrameworkTestRepository.Project("<TargetFramework>net10.0</TargetFramework>")),
            ("tools/B/B.csproj", TargetFrameworkTestRepository.Project("<TargetFramework>net10.0</TargetFramework><RollForward>Major</RollForward>"))));

        // Act
        var result = await TargetFrameworkTestRepository.Run(repository);

        // Assert
        result.ExitCode.ShouldBe(1);
        TargetFrameworkTestRepository.ErrorLines(result).ShouldBe(
        [
            Heading,
            "- tools/A/A.csproj: RollForward is 'LatestMajor', but the rule 'tools/*.csproj' expects 'Major'.",
        ],
        "Only the targets file in the directory of A applies, and B sets the expected value itself.");
    }

    [Fact]
    public async Task Verify_target_frameworks_applies_simple_conditions()
    {
        // Arrange
        using var repository = await TargetFrameworkTestRepository.Create(TargetFrameworkTestRepository.Files(
            ("Directory.Build.props", TargetFrameworkTestRepository.Props("""
                <PropertyGroup Condition="'$(Missing)' != ''">
                  <TargetFrameworks>net9.0</TargetFrameworks>
                </PropertyGroup>
                <PropertyGroup Condition="'$(Configuration)' != 'Debug' AND '$(Missing)' == ''">
                  <TargetFrameworks Condition="'$(TargetFrameworks)' == ''">net10.0;net11.0</TargetFrameworks>
                </PropertyGroup>
                <PropertyGroup Condition="'$(Missing)' == 'x' or '$(Missing)' == 'y'">
                  <TargetFrameworks>net8.0</TargetFrameworks>
                </PropertyGroup>
                """)),
            ("app/App.csproj", TargetFrameworkTestRepository.Project(string.Empty))));

        // Act
        var result = await TargetFrameworkTestRepository.Run(repository);

        // Assert
        result.StandardError.ShouldBe(string.Empty);
        result.ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task Verify_target_frameworks_reports_a_condition_that_it_cannot_evaluate()
    {
        // Arrange
        using var repository = await TargetFrameworkTestRepository.Create(TargetFrameworkTestRepository.Files(
            ("Directory.Build.props", TargetFrameworkTestRepository.Props("""
                <PropertyGroup>
                  <TargetFrameworks Condition="Exists('x.props')">net11.0</TargetFrameworks>
                  <TargetFramework>$([MSBuild]::Unknown())</TargetFramework>
                  <Other Condition="Exists('y')">z</Other>
                </PropertyGroup>
                """)),
            ("app/App.csproj", TargetFrameworkTestRepository.Project(string.Empty))));

        // Act
        var result = await TargetFrameworkTestRepository.Run(repository);

        // Assert
        result.ExitCode.ShouldBe(1);
        TargetFrameworkTestRepository.ErrorLines(result).ShouldBe(
        [
            Heading,
            "- Directory.Build.props: Cannot evaluate the condition of TargetFrameworks; the check reads only '…' == '…' conditions.",
            "- Directory.Build.props: Cannot evaluate the value of TargetFramework; the check reads only text and $(Property) references.",
        ]);
    }

    [Fact]
    public async Task Verify_target_frameworks_reports_a_props_file_that_is_not_XML()
    {
        // Arrange
        using var repository = await TargetFrameworkTestRepository.Create(TargetFrameworkTestRepository.Files(
            ("Directory.Build.props", "<Project>"),
            ("app/App.csproj", TargetFrameworkTestRepository.Project(Multi))));

        // Act
        var result = await TargetFrameworkTestRepository.Run(repository);

        // Assert
        result.ExitCode.ShouldBe(1);
        TargetFrameworkTestRepository.ErrorLines(result).ShouldBe([Heading, "- Directory.Build.props: Is not valid XML."]);
    }

    [Fact]
    public async Task Verify_target_frameworks_reports_a_lock_file_for_other_frameworks()
    {
        // Arrange
        using var repository = await TargetFrameworkTestRepository.Create(TargetFrameworkTestRepository.Files(
            ("src/Lib/Lib.csproj", TargetFrameworkTestRepository.Project(Multi)),
            ("src/Lib/packages.lock.json", TargetFrameworkTestRepository.Lock("net10.0"))));

        // Act
        var result = await TargetFrameworkTestRepository.Run(repository);

        // Assert
        result.ExitCode.ShouldBe(1);
        TargetFrameworkTestRepository.ErrorLines(result).ShouldBe(
        [
            Heading,
            "- src/Lib/packages.lock.json: Locks net10.0, but src/Lib/Lib.csproj builds net10.0;net11.0; restore without --locked-mode to regenerate it.",
        ]);
    }

    [Fact]
    public async Task Verify_target_frameworks_normalizes_the_frameworks_that_a_lock_file_names()
    {
        // Arrange
        using var repository = await TargetFrameworkTestRepository.Create(
            TargetFrameworkTestRepository.Files(
                ("src/Lib/Lib.csproj", TargetFrameworkTestRepository.Project("<TargetFrameworks>netstandard2.0;net10.0;net11.0;net48</TargetFrameworks>")),
                ("src/Lib/packages.lock.json", TargetFrameworkTestRepository.Lock(
                    ".NETStandard,Version=v2.0",
                    ".NETCoreApp,Version=v10.0",
                    "net11.0/linux-x64",
                    ".NETFramework,Version=v4.8"))),
            TargetFrameworkTestRepository.Configuration(
                """[{ "include": "*.csproj", "frameworks": ["netstandard2.0", "net10.0", "net11.0", "net48"] }]"""));

        // Act
        var result = await TargetFrameworkTestRepository.Run(repository);

        // Assert
        result.StandardError.ShouldBe(string.Empty);
        result.ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task Verify_target_frameworks_reports_a_framework_that_the_pinned_sdk_cannot_build()
    {
        // Arrange
        using var repository = await TargetFrameworkTestRepository.Create(TargetFrameworkTestRepository.Files(
            ("global.json", """{"sdk":{"version":"10.0.100","rollForward":"latestFeature"}}"""),
            ("a/A.csproj", TargetFrameworkTestRepository.Project(Multi)),
            ("b/B.csproj", TargetFrameworkTestRepository.Project(Multi))));

        // Act
        var result = await TargetFrameworkTestRepository.Run(repository);

        // Assert
        result.ExitCode.ShouldBe(1);
        TargetFrameworkTestRepository.ErrorLines(result).ShouldBe(
            [Heading, "- global.json: SDK 10.0.100 cannot build net11.0 (first required by a/A.csproj); pin an SDK 11.0.100 or later."]);
    }

    [Fact]
    public async Task Verify_target_frameworks_checks_the_dotnet_run_lines_of_command_files()
    {
        // Arrange
        using var repository = await TargetFrameworkTestRepository.Create(TargetFrameworkTestRepository.Files(
            ("src/Lib/Lib.csproj", TargetFrameworkTestRepository.Project(Multi)),
            ("src/Lib/packages.lock.json", TargetFrameworkTestRepository.Lock("net10.0", "net11.0")),
            ("run.sh", string.Join(
                '\n',
                "dotnet run --project src/Lib",
                "dotnet run --project ./src/Lib/Lib.csproj -f net10.0",
                "dotnet run --project src/Lib -f=net10.0",
                "dotnet run --project \"$TOOL\"",
                "dotnet run --project src/Missing",
                "dotnet run \\",
                "    --project 'src\\Lib/Lib.csproj' \\",
                "    -- --help",
                string.Empty)),
            (".github/ci.yml", "run: dotnet run --project src/Lib/Lib.csproj --framework net11.0\n"),
            ("README.md", "Run `dotnet run --project src/Lib` first.\n")));

        // Act
        var result = await TargetFrameworkTestRepository.Run(repository);

        // Assert
        result.ExitCode.ShouldBe(1);
        TargetFrameworkTestRepository.ErrorLines(result).ShouldBe(
        [
            Heading,
            "- README.md: Line 1: dotnet run --project src/Lib needs --framework because the project builds net10.0;net11.0.",
            "- run.sh: Line 1: dotnet run --project src/Lib needs --framework because the project builds net10.0;net11.0.",
            "- run.sh: Line 6: dotnet run --project src/Lib/Lib.csproj needs --framework because the project builds net10.0;net11.0.",
        ],
        "Findings come in file order, then line order, and a path that is not a tracked project is skipped.");
    }

    [Fact]
    public async Task Verify_target_frameworks_lists_project_findings_before_sdk_and_command_file_findings()
    {
        // Arrange
        using var repository = await TargetFrameworkTestRepository.Create(TargetFrameworkTestRepository.Files(
            ("global.json", """{"sdk":{"version":"10.0.100"}}"""),
            ("b/B.csproj", TargetFrameworkTestRepository.Project(Multi)),
            ("a/A.csproj", TargetFrameworkTestRepository.Project("<TargetFramework>net10.0</TargetFramework>")),
            ("run.sh", "dotnet run --project b\n")));

        // Act
        var result = await TargetFrameworkTestRepository.Run(repository);

        // Assert
        result.ExitCode.ShouldBe(1);
        TargetFrameworkTestRepository.ErrorLines(result).ShouldBe(
        [
            Heading,
            "- a/A.csproj: Declares net10.0, but the rule '*.csproj' expects net10.0;net11.0.",
            "- global.json: SDK 10.0.100 cannot build net11.0 (first required by b/B.csproj); pin an SDK 11.0.100 or later.",
            "- run.sh: Line 1: dotnet run --project b needs --framework because the project builds net10.0;net11.0.",
        ]);
    }

    [Fact]
    public async Task Verify_target_frameworks_fails_without_a_target_frameworks_section()
    {
        // Arrange
        using var repository = await TargetFrameworkTestRepository.Create(
            TargetFrameworkTestRepository.Files(("a/A.csproj", TargetFrameworkTestRepository.Project(Multi))),
            """{"version":1}""");

        // Act
        var result = await TargetFrameworkTestRepository.Run(repository);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe("sharedkernel-repo: The consumer configuration has no targetFrameworks section." + Environment.NewLine);
    }

    [Theory]
    [InlineData("""{"version":1,"targetFrameworks":{}}""", " is not valid at $.targetFrameworks (line 1).")]
    [InlineData("""{"version":1,"targetFrameworks":{"projects":[]}}""", ": $.targetFrameworks.projects must name at least one rule.")]
    [InlineData("""{"version":1,"targetFrameworks":{"projects":[{"include":"a\\b","frameworks":["net10.0"]}]}}""", ": $.targetFrameworks.projects[0].include must be a repository-relative path pattern with '/' separators and without empty, '.', or '..' segments.")]
    [InlineData("""{"version":1,"targetFrameworks":{"projects":[{"include":"*.csproj","frameworks":[]}]}}""", ": $.targetFrameworks.projects[0].frameworks must name at least one target framework.")]
    [InlineData("""{"version":1,"targetFrameworks":{"projects":[{"include":"*.csproj","frameworks":["net10.0","NET10.0"]}]}}""", ": $.targetFrameworks.projects[0].frameworks[1] repeats a target framework.")]
    [InlineData("""{"version":1,"targetFrameworks":{"projects":[{"include":"*.csproj","frameworks":["net10.0","10 net"]}]}}""", ": $.targetFrameworks.projects[0].frameworks[1] must be a target framework moniker such as net10.0 or netstandard2.0.")]
    [InlineData("""{"version":1,"targetFrameworks":{"projects":[{"include":"*.csproj","frameworks":["net10.0"],"rollForward":"Sideways"}]}}""", ": $.targetFrameworks.projects[0].rollForward must be one of Minor, Major, LatestPatch, LatestMinor, LatestMajor, Disable.")]
    [InlineData("""{"version":1,"targetFrameworks":{"projects":[{"include":"*.csproj","frameworks":["net10.0"]}],"commandFiles":["ok","../x"]}}""", ": $.targetFrameworks.commandFiles[1] must be a repository-relative path pattern with '/' separators and without empty, '.', or '..' segments.")]
    public async Task Verify_target_frameworks_rejects_an_invalid_target_frameworks_section(string configuration, string expectedMessage)
    {
        // Arrange
        using var repository = await TargetFrameworkTestRepository.Create(
            TargetFrameworkTestRepository.Files(("a/A.csproj", TargetFrameworkTestRepository.Project(Multi))),
            configuration);

        // Act
        var result = await TargetFrameworkTestRepository.Run(repository);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldStartWith("sharedkernel-repo: .config/sharedkernel-repo.json" + expectedMessage, StringComparison.Ordinal);
        result.StandardError.ShouldEndWith(Environment.NewLine, StringComparison.Ordinal);
    }
}
