using System.IO.Compression;
using System.Security.Cryptography;
using SharedKernel.Testing;

namespace SharedKernel.Mediator.PackageConsumptionTests;

/// <summary>Verifies the compiler assemblies embedded by NuGet pack.</summary>
[Trait(TestTraitNames.CategoryName, "PackageConsumption")]
public sealed class CompilerPackageOutputTests
{
    [Theory]
    [InlineData("Results", "Results.SourceGenerator")]
    [InlineData("Testing", "Testing.SourceGenerator")]
    [InlineData("Testing.CodeFixes", "Testing.Analyzers")]
    [InlineData("Style.CodeFixes", "Style.Analyzers")]
    [InlineData("AuditTrail.SourceGenerator", "AuditTrail.SourceGenerator")]
    public async Task Pack_includes_the_compiler_asset_from_the_requested_output(string packageProject, string compilerProject)
    {
        // Arrange
        using var temporary = new TemporaryDirectory();
        var repository = RepositoryRoot.Locate("SharedKernel.slnx");
        var projectName = $"SharedKernel.{packageProject}";
        var compilerName = $"SharedKernel.{compilerProject}";
        var project = Path.Combine(repository, "src", "SharedKernel", projectName, $"{projectName}.csproj");
        var artifacts = Path.Combine(temporary.Path, "build outputs");
        var fixtureInventoryRoot = Path.Combine(artifacts, "governed-literals");
        var feed = Path.Combine(temporary.Path, "feed");
        const string packageVersion = "1.0.0-test-compiler-output";
        var cancellationToken = TestContext.Current.CancellationToken;
        var inventory = Path.Combine(repository, "TestResults", "governed-literals", "Release", compilerName, "netstandard2.0.inputs");
        var originalInventory = File.Exists(inventory) ? await File.ReadAllTextAsync(inventory, cancellationToken) : null;

        // Act
        await DotNetCli.Run(repository, "pack", project, "-c", "Release", "--disable-build-servers",
            "--artifacts-path", artifacts, "-o", feed, $"-p:PackageVersion={packageVersion}", "-p:AssemblyVersion=42.1.2.3",
            "-p:REMOTE_CONTAINERS=false",
            "-p:GovernedLiteralInputsRoot=" + fixtureInventoryRoot);

        // Assert
        var compilerOutput = Directory.EnumerateFiles(Path.Combine(artifacts, "bin", compilerName), $"{compilerName}.dll", SearchOption.AllDirectories)
            .ShouldHaveSingleItem();
        await using var expected = File.OpenRead(compilerOutput);
        var packagePath = Path.Combine(feed, $"PandaBear.{projectName}.{packageVersion}.nupkg");
        await using var package = await ZipFile.OpenReadAsync(packagePath, cancellationToken);
        var entry = package.GetEntry($"analyzers/dotnet/cs/{compilerName}.dll").ShouldNotBeNull();
        await using var actual = await entry.OpenAsync(cancellationToken);
        var expectedHash = await SHA256.HashDataAsync(expected, cancellationToken);
        var actualHash = await SHA256.HashDataAsync(actual, cancellationToken);
        actualHash.ShouldBe(expectedHash);
        var finalInventory = File.Exists(inventory) ? await File.ReadAllTextAsync(inventory, cancellationToken) : null;
        finalInventory.ShouldBe(originalInventory, "the temporary pack must preserve the conventional compiler-input inventory");
        var fixtureInventory = Path.Combine(fixtureInventoryRoot, "Release", compilerName, "netstandard2.0.inputs");
        File.Exists(fixtureInventory).ShouldBeTrue("the temporary pack records its own compiler-input inventory");
    }
}
