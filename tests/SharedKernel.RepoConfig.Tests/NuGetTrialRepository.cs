using PandaBear.Testing.Packaging;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Creates a disposable CPM solution using only small packages from its mapped local source.</summary>
internal sealed class NuGetTrialRepository : IDisposable
{
    /// <summary>The imported central versions.</summary>
    internal const string VersionsPath = "eng/Versions.props";
    /// <summary>The only tracked package lock.</summary>
    internal const string LockedPath = "src/locked/packages.lock.json";
    /// <summary>The file-based fixture outside the solution.</summary>
    internal const string FileAppPath = "operations/check.cs";
    private const string FeedPath = ".local/packages";

    private readonly TemporaryGitRepository repository = new();

    /// <summary>The source checkout root.</summary>
    internal string Root => repository.RootPath;

    /// <summary>Creates and restores the fixture solution before committing the source snapshot.</summary>
    internal async Task Initialize(bool directCore = false, NuGetTrialAuditFeed? audit = null, string? minimum = null,
        bool fileCore = false, bool nestedFileVersions = false, bool trackedFileLock = false, bool fileAppAdvisory = false,
        bool fileAppSourceUnavailable = false, bool baselineFileAppAdvisory = false)
    {
        var ct = TestContext.Current.CancellationToken;
        await repository.Initialize(ct);
        repository.WriteText(".gitignore", ".local/\n**/obj/\n**/bin/\n");
        repository.WriteText("global.json", """{"sdk":{"version":"11.0.100","rollForward":"disable"}}""");
        var auditSources = audit is null ? string.Empty :
            $"<auditSources><clear/><add key=\"audit\" value=\"{audit.IndexUri.AbsoluteUri}\" allowInsecureConnections=\"true\" /></auditSources>";
        var alternate = minimum is null ? string.Empty : $"""<add key="unmapped" value="{audit!.UnmappedUri.AbsoluteUri}" allowInsecureConnections="true"/>""";
        var cache = minimum is "unavailable" or "ambiguous" ? Path.Combine(Root, ".local", "cache") : ".local/cache";
        var config = $"""
            <configuration>
              <packageSources><clear/><add key="fixtures" value=".local/packages"/>{alternate}</packageSources>
              <packageSourceMapping>
                <packageSource key="fixtures"><package pattern="PandaBear.NuGetTrialFixture.*"/></packageSource>
                <packageSource key="unmapped"><package pattern="Unrelated.*"/></packageSource>
              </packageSourceMapping>
              <config><add key="globalPackagesFolder" value="{cache}"/></config>
              {auditSources}
            </configuration>
            """;
        repository.WriteText("NuGet.Config", config);
        repository.WriteText("Example.slnx", """
            <Solution>
              <Project Path="src/locked/Locked.csproj" />
              <Project Path="src/exempt/Exempt.csproj" />
            </Solution>
            """);
        if (minimum == "toleratedSourceWarning")
        {
            repository.WriteText("Directory.Solution.targets", """
                <Project>
                  <Target Name="FixtureSourceAvailability" BeforeTargets="Restore">
                    <XmlPeek XmlInputPath="$(MSBuildThisFileDirectory)Directory.Packages.props"
                             Query="/Project/ItemGroup/PackageVersion[@Include='PandaBear.NuGetTrialFixture.Driver']/@Version">
                      <Output TaskParameter="Result" PropertyName="FixtureDriverVersion" />
                    </XmlPeek>
                    <Warning Condition="'$(FixtureDriverVersion)' == '1.1.0'" Code="NU1801"
                             Text="The fixture source became unavailable for this candidate." />
                  </Target>
                </Project>
                """);
        }

        var versions = """
            <Project>
              <!-- Keep this comment and the single quotes. -->
              <ItemGroup>
                <PackageVersion Include='PandaBear.NuGetTrialFixture.Driver' Version='1.0.0'/>
                <PackageVersion Include='PandaBear.NuGetTrialFixture.Partner' Version='1.0.0'/>
                <PackageVersion Include='PandaBear.NuGetTrialFixture.Core' Version='2.0.0'/>
              </ItemGroup>
            </Project>
            """;
        if (minimum is "coupled")
        {
            versions = versions.Replace("Partner' Version='1.0.0'", "Partner' Version='[1.0.0]'", StringComparison.Ordinal);
        }

        repository.WriteText("Directory.Packages.props", audit is null ? """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
              </PropertyGroup>
              <Import Project="eng/Versions.props" />
            </Project>
            """ : versions.Replace("<ItemGroup>", """
              <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
              <ItemGroup>
            """, StringComparison.Ordinal)
            .Replace("    <PackageVersion Include='PandaBear.NuGetTrialFixture.Core' Version='2.0.0'/>\n", string.Empty, StringComparison.Ordinal));
        var importedVersions = versions.Replace("Partner' Version='1.0.0'", "Partner' Version='[1.0.0]'", StringComparison.Ordinal);
        repository.WriteText(VersionsPath, importedVersions.Replace("\n", "\r\n", StringComparison.Ordinal));
        if (fileCore)
        {
            repository.WriteText(FileAppPath, """
                #:package PandaBear.NuGetTrialFixture.Core
                #:property NuGetAudit=false
                #:property PublishAot=false
                #:property RestorePackagesWithLockFile=true
                throw new System.InvalidOperationException("The fixture application must never execute.");
                """);
        }

        if (fileAppAdvisory || baselineFileAppAdvisory)
        {
            repository.WriteText(FileAppPath, $$"""
                #:package {{Driver}}
                #:property NuGetAudit=true
                #:property NuGetAuditMode=all
                #:property NuGetAuditLevel=high
                #:property PublishAot=false
                #:property TreatWarningsAsErrors=false
                throw new System.InvalidOperationException("The fixture application must never execute.");
                """);
        }

        if (fileAppAdvisory)
        {
            repository.WriteText("operations/Directory.Build.props", $"""
                <Project>
                  <ItemGroup><NuGetAuditSuppress Include="{NuGetTrialAuditFeed.Advisory}" /></ItemGroup>
                </Project>
                """);
        }

        if (fileAppSourceUnavailable)
        {
            repository.WriteText("operations/Directory.Build.targets", """
                <Project>
                  <Target Name="FixtureFileAppSourceAvailability" BeforeTargets="Restore">
                    <Warning Code="NU1801" Text="The file app fixture source is unavailable." />
                  </Target>
                </Project>
                """);
        }

        if (nestedFileVersions)
        {
            repository.WriteText("operations/Directory.Packages.props", """
                <Project>
                  <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
                  <ItemGroup><PackageVersion Include="PandaBear.NuGetTrialFixture.Core" Version="1.0.0" /></ItemGroup>
                </Project>
                """);
        }

        foreach (var (folder, project, useLock) in new[] { ("locked", "Locked", "true"), ("exempt", "Exempt", "false") })
        {
            var core = directCore ? """<PackageReference Include="PandaBear.NuGetTrialFixture.Core" />""" : string.Empty;
            var suppressedAdvisory = fileAppAdvisory ? NuGetTrialAuditFeed.FileAppAdvisory
                : baselineFileAppAdvisory ? NuGetTrialAuditFeed.Advisory : null;
            var suppression = suppressedAdvisory is null ? string.Empty : $"""<NuGetAuditSuppress Include="{suppressedAdvisory}" />""";
            repository.WriteText($"src/{folder}/{project}.csproj", $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net11.0</TargetFramework>
                    <NuGetAudit>{(audit is null ? "false" : "true")}</NuGetAudit>
                    <NuGetAuditMode>all</NuGetAuditMode>
                    <NuGetAuditLevel>high</NuGetAuditLevel>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                    <RestorePackagesWithLockFile>{useLock}</RestorePackagesWithLockFile>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="PandaBear.NuGetTrialFixture.Driver" />
                    <PackageReference Include="PandaBear.NuGetTrialFixture.Partner" />
                    {core}
                    {suppression}
                  </ItemGroup>
                </Project>
                """);
        }

        var feed = Path.Combine(Root, FeedPath);
        _ = NuGetPackageBuilder.WritePackage(feed, Core, "1.0.0");
        _ = NuGetPackageBuilder.WritePackage(feed, Core, "2.0.0");
        _ = NuGetPackageBuilder.WritePackage(feed, Driver, "1.0.0", (Core, "[1.0.0,)"));
        var ownerConstraints = audit is null ? new[] { (Partner, "[2.0.0]"), (Core, "[2.0.0,)") } : [(Core, "[2.0.0,)")];
        if (minimum is not "coupled")
        {
            _ = NuGetPackageBuilder.WritePackage(feed, Driver, "2.0.0", ownerConstraints);
        }
        if (minimum is "coupled")
        {
            _ = NuGetPackageBuilder.WritePackage(feed, Driver, "1.1.0", (Core, "[2.0.0,)"), (Partner, "[1.1.0]"));
            _ = NuGetPackageBuilder.WritePackage(feed, Partner, "1.1.0");
        }
        else if (minimum is "tied")
        {
            _ = NuGetPackageBuilder.WritePackage(feed, Driver, "1.1.0", (Core, "[2.0.0,)"));
            _ = NuGetPackageBuilder.WritePackage(feed, Partner, "1.1.0", (Core, "[2.0.0,)"));
        }
        else if (minimum is not null)
        {
            _ = NuGetPackageBuilder.WritePackage(feed, Driver, "1.1.0", (Core, "[1.0.0,)"));
            _ = NuGetPackageBuilder.WritePackage(feed, Driver, "1.2.0", (Core, "[2.0.0,)"));
        }

        if (minimum is "tied")
        {
            _ = NuGetPackageBuilder.WritePackage(feed, Partner, "1.0.0", (Core, "[1.0.0,)"));
        }
        else
        {
            _ = NuGetPackageBuilder.WritePackage(feed, Partner, "1.0.0");
        }

        _ = NuGetPackageBuilder.WritePackage(feed, Partner, "2.0.0");

        var restore = await TestProcess.Run("dotnet", Root,
            ["restore", "Example.slnx", "--force-evaluate", "--disable-build-servers", "-p:NuGetAudit=false"], ct);
        if (restore.ExitCode != 0)
        {
            throw new InvalidOperationException(restore.StandardOutput + restore.StandardError);
        }

        if (trackedFileLock)
        {
            var fileRestore = await TestProcess.Run("dotnet", Root,
                ["restore", FileAppPath, "--force-evaluate", "--disable-build-servers"], ct);
            fileRestore.ExitCode.ShouldBe(0, fileRestore.StandardOutput + fileRestore.StandardError);
        }

        if (minimum is "unavailable" or "ambiguous")
        {
            if (minimum == "ambiguous")
            {
                var second = Path.Combine(Root, ".local", "unavailable");
                Directory.CreateDirectory(second);
                _ = NuGetPackageBuilder.WritePackage(second, Driver, "1.2.0", (Core, "[2.0.0,)"));
            }

            config = config.Replace("</packageSources>", """<add key="missing" value=".local/unavailable"/></packageSources>""", StringComparison.Ordinal)
                .Replace("</packageSourceMapping>", """<packageSource key="missing"><package pattern="PandaBear.NuGetTrialFixture.*"/></packageSource></packageSourceMapping>""", StringComparison.Ordinal);
            repository.WriteText("NuGet.Config", config);
        }

        var stage = await repository.RunGit(["add", "--all"], ct);
        stage.ExitCode.ShouldBe(0);
        var commit = await repository.RunGit(
            ["-c", "user.name=Trial fixture", "-c", "user.email=trial@example.invalid", "-c", "commit.gpgsign=false",
                "commit", "--quiet", "-m", "Fixture"], ct);
        commit.ExitCode.ShouldBe(0);
    }

    /// <summary>Refreshes the restore evidence using the comparison command's locked evaluation.</summary>
    internal async Task RestoreLocked()
    {
        var restore = await TestProcess.Run("dotnet", Root,
            ["restore", "Example.slnx", "--locked-mode", "--force", "--disable-build-servers"], TestContext.Current.CancellationToken);
        restore.ExitCode.ShouldBe(0, restore.StandardOutput + restore.StandardError);
    }

    /// <summary>The provider package that owns the dependency path.</summary>
    internal const string Driver = "PandaBear.NuGetTrialFixture.Driver";
    /// <summary>The direct package that must move with the provider.</summary>
    internal const string Partner = "PandaBear.NuGetTrialFixture.Partner";
    /// <summary>The package that starts as a transitive pin.</summary>
    internal const string Core = "PandaBear.NuGetTrialFixture.Core";

    /// <summary>Invokes the trial against the local feed and imported versions.</summary>
    internal Task<TemporaryGitRepository.CommandResult> RunTrial(string plan, params string[] arguments) =>
        Run(plan, ["--props", VersionsPath, "--remove-pin", Core, .. arguments]);

    /// <summary>Invokes the SDK advisory proposal against the synthetic sources.</summary>
    internal Task<TemporaryGitRepository.CommandResult> RunAudit(string plan, params string[] arguments) => Run(plan, ["--vulnerable", .. arguments]);

    private Task<TemporaryGitRepository.CommandResult> Run(string plan, string[] arguments) =>
        RunDependency("trial-upgrade", ["--plan", plan, "--copy-local-source", FeedPath, .. arguments]);

    /// <summary>Runs a dependency subcommand against the restored Git fixture.</summary>
    internal async Task<TemporaryGitRepository.CommandResult> RunDependency(string command, params string[] arguments)
    {
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        var exitCode = await RepoConfigToolRunner.Run(
            ["dependency", command, "--root", Root, .. arguments],
            output, error, Root, TestContext.Current.CancellationToken);
        return new(exitCode, output.ToString(), error.ToString());
    }

    /// <summary>Checks or applies the reviewed patch in the source fixture.</summary>
    internal Task<TestProcessResult> Git(params string[] arguments) =>
        repository.RunGit(arguments, TestContext.Current.CancellationToken);

    /// <inheritdoc />
    public void Dispose() => repository.Dispose();
}
