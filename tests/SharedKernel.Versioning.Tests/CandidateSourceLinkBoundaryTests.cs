using System.Text.Json.Nodes;

namespace SharedKernel.Versioning.Tests;

/// <summary>Verifies input boundaries for the offline Source Link command.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class CandidateSourceLinkBoundaryTests
{
    private const string Host = "https://content.example.test";
    private const string MapRoot = Host + "/owner/repo/" + CandidateSourceLinkTestFeed.CandidateRevision + "/";

    [Theory]
    [InlineData(Host + "/owner/repo/fixed/*")]
    [InlineData(Host + "/owner/{revision}/repo/{revision}/*")]
    [InlineData(Host + "/owner/repo/{revision}/main.cs")]
    [InlineData(Host + "/owner/repo/{revision}/*/*")]
    [InlineData("/owner/repo/{revision}/*")]
    [InlineData("http://content.example.test/owner/repo/{revision}/*")]
    [InlineData("https://user@content.example.test/owner/repo/{revision}/*")]
    [InlineData(Host + "/owner/repo/{revision}/?query=/*")]
    [InlineData(Host + "/owner/repo/{revision}/#fragment/*")]
    [InlineData(Host + "/owner/repo/{revision}/../other/*")]
    public static async Task Command_refuses_an_unbound_or_invalid_source_pattern(string pattern)
    {
        // Arrange
        using var feed = new CandidateSourceLinkTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var (manifest, symbols) = await feed.Write(CandidateSourceLinkTestFeed.CandidateRevision, mismatchedAssembly: false, ct);
        var report = Path.Combine(feed.DirectoryPath, "source-link-report.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["verify-candidate-source-link", "--candidate", manifest, "--symbols", symbols,
                "--source-url-pattern", pattern, "--output", report], ct);

        // Assert
        result.ExitCode.ShouldBe(2, result.Error);
        result.Error.ShouldContain("Source URL pattern must be HTTPS", StringComparison.Ordinal);
        File.Exists(report).ShouldBeFalse();
    }

    [Theory]
    [InlineData("{", "invalid-source-link")]
    [InlineData("[]", "invalid-source-link")]
    [InlineData("{}", "invalid-source-link")]
    [InlineData("""{"documents":[]}""", "invalid-source-link")]
    [InlineData("""{"documents":{}}""", "invalid-source-link")]
    [InlineData("""{"documents":{"":""}"}""", "invalid-source-link")]
    [InlineData("""{"documents":{"/_/*/Probe.cs":""}}""", "invalid-source-link")]
    [InlineData("""{"documents":{"/_/*":false}}""", "invalid-source-link")]
    [InlineData("""{"documents":{"/_/*":"{source}*","/_/*":"{source}*"}}""", "invalid-source-link")]
    [InlineData("""{"documents":{"/_/*":"{source}Probe.cs"}}""", "invalid-source-link")]
    [InlineData("""{"documents":{"/_/*":"./*"}}""", "invalid-source-link")]
    [InlineData("""{"documents":{"/_/*":"https://another.example.test/*"}}""", "source-link-mismatch")]
    [InlineData("""{"documents":{"/_/*":"https://content.example.test:8443/*"}}""", "source-link-mismatch")]
    [InlineData("""{"documents":{"/_/src/Probe.cs":"{source}src/Probe.cs"}}""", "passed")]
    public static async Task Command_reports_the_source_mapping_result(string mapping, string status)
    {
        // Arrange
        using var feed = new CandidateSourceLinkTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var (manifest, symbols) = await feed.Write(sourceRevision: null, mismatchedAssembly: false, ct,
            sourceLinkJson: mapping.ShouldNotBeNull().Replace("{source}", MapRoot, StringComparison.Ordinal));
        var report = Path.Combine(feed.DirectoryPath, "source-link-report.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["verify-candidate-source-link", "--candidate", manifest, "--symbols", symbols,
                "--source-url-pattern", CandidateSourceLinkTestFeed.SourcePattern, "--output", report], ct);

        // Assert
        result.ExitCode.ShouldBe(status == "passed" ? 0 : 3, result.Error);
        var evidence = JsonNode.Parse(await File.ReadAllTextAsync(report, ct)).ShouldNotBeNull();
        evidence["symbols"]!.AsArray().ShouldHaveSingleItem().ShouldNotBeNull()["status"]!.GetValue<string>().ShouldBe(status);
    }

    [Theory]
    [InlineData("", "has no archives")]
    [InlineData("invalid  {symbol}\n", "requires a SHA-256 digest")]
    [InlineData("{invalidSha256}  {symbol}\n", "requires a SHA-256 digest")]
    [InlineData("{sha256}  wrong.nupkg\n", "invalid or duplicate archive filename")]
    [InlineData("{sha256}  ./../nested.snupkg\n", "invalid or duplicate archive filename")]
    [InlineData("{sha256}  {symbol}\n{sha256}  ./{symbol}\n", "invalid or duplicate archive filename")]
    [InlineData("{sha256}  {symbol}\n", "does not match its recorded hash")]
    public static async Task Command_refuses_an_invalid_symbol_inventory(string template, string message)
    {
        // Arrange
        using var feed = new CandidateSourceLinkTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var (manifest, symbols) = await feed.Write(CandidateSourceLinkTestFeed.CandidateRevision, mismatchedAssembly: false, ct);
        var inventory = template.ShouldNotBeNull().Replace("{symbol}", CandidateSourceLinkTestFeed.SymbolFileName, StringComparison.Ordinal)
            .Replace("{sha256}", new string('0', 64), StringComparison.Ordinal)
            .Replace("{invalidSha256}", new string('z', 64), StringComparison.Ordinal);
        await File.WriteAllTextAsync(symbols, inventory, ct);
        var report = Path.Combine(feed.DirectoryPath, "source-link-report.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["verify-candidate-source-link", "--candidate", manifest, "--symbols", symbols,
                "--source-url-pattern", CandidateSourceLinkTestFeed.SourcePattern, "--output", report], ct);

        // Assert
        result.ExitCode.ShouldBe(2, result.Error);
        result.Error.ShouldContain(message, StringComparison.Ordinal);
        File.Exists(report).ShouldBeFalse();
    }

    [Fact]
    public static async Task Command_reports_a_missing_matching_assembly()
    {
        // Arrange
        using var feed = new CandidateSourceLinkTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var (manifest, symbols) = await feed.Write(CandidateSourceLinkTestFeed.CandidateRevision, mismatchedAssembly: false,
            ct, symbolMember: "Unpaired.pdb");
        var report = Path.Combine(feed.DirectoryPath, "source-link-report.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["verify-candidate-source-link", "--candidate", manifest, "--symbols", symbols,
                "--source-url-pattern", CandidateSourceLinkTestFeed.SourcePattern, "--output", report], ct);

        // Assert
        result.ExitCode.ShouldBe(3, result.Error);
        var evidence = JsonNode.Parse(await File.ReadAllTextAsync(report, ct)).ShouldNotBeNull();
        evidence["symbols"]!.AsArray().ShouldHaveSingleItem().ShouldNotBeNull()["status"]!.GetValue<string>()
            .ShouldBe("missing-assembly");
    }
}
