using PandaBear.Processes;

namespace PandaBear.Repositories.Tests;

/// <summary>Tests the report boundary for SDK package audit diagnostics.</summary>
public sealed class NuGetTrialDiagnosticTests
{
    [Fact]
    public void Audit_evidence_keeps_the_project_package_and_advisory_without_source_details()
    {
        // Arrange
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "trial-diagnostics"));
        var project = Path.Combine(root, "src", "Example.csproj");
        var text = $"{project} : error NU1903: Warning As Error: Package 'Fixture.Provider' 1.2.3 has a known high severity vulnerability, https://example.invalid/advisories/id?ignored=sensitive";
        var process = new ProcessOutput(1, text + "\n" + text, string.Empty);

        // Act
        var step = NuGetTrialStep.Read("baselineLockedRestore", root, process);

        // Assert
        step.ExitCode.ShouldBe(1);
        step.AuditUnavailable.ShouldBeFalse();
        step.Codes.ShouldBe(["NU1903"]);
        step.Errors.ShouldBe(["NU1903"]);
        var advisory = step.Advisories.ShouldHaveSingleItem();
        advisory.Package.ShouldBe("Fixture.Provider");
        advisory.Version.ShouldBe("1.2.3");
        advisory.Advisory.ShouldBe("https://example.invalid/advisories/id");
        advisory.Project.ShouldBe("src/Example.csproj");
    }

    [Theory]
    [InlineData("operations/check.cs.csproj", "operations/check.cs")]
    [InlineData("other/check.cs.csproj", "other/check.cs.csproj")]
    [InlineData("operations/check.csproj", "operations/check.csproj")]
    [InlineData("../external/check.cs.csproj", null)]
    public void File_app_audit_uses_only_the_evaluated_virtual_project_identity(string reportedProject, string? expectedProject)
    {
        // Arrange
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "trial-file-app-diagnostics"));
        var project = Path.GetFullPath(reportedProject, root);
        var text = $"{project} : warning NU1903: Package 'Fixture.Provider' 1.2.3 has a known high severity vulnerability, https://example.invalid/advisories/id";
        var process = new ProcessOutput(0, text);
        var fileApp = new NuGetFileApp("operations/check.cs", "operations/check.cs.csproj", "net11.0",
            new Dictionary<string, (string?, string?)>(StringComparer.OrdinalIgnoreCase), false, null);

        // Act
        var step = NuGetTrialStep.Read("baselineFileAppLockedRestore", root, process, fileApp);

        // Assert
        var advisory = step.Advisories.ShouldHaveSingleItem();
        advisory.Project.ShouldBe(expectedProject);
    }

    [Theory]
    [InlineData("NU1900", true)]
    [InlineData("NU1905", true)]
    [InlineData("NU1803", false)]
    public void Audit_source_failures_have_a_distinct_disposition(string code, bool unavailable)
    {
        // Arrange
        var root = Path.GetTempPath();
        var process = new ProcessOutput(0, $"warning {code}: SDK diagnostic", string.Empty);

        // Act
        var step = NuGetTrialStep.Read("candidateLockedRestore", root, process);

        // Assert
        step.AuditUnavailable.ShouldBe(unavailable);
        step.Advisories.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("error NU1301: source failed", true)]
    [InlineData("warning NU1801: configured source failed", true)]
    [InlineData("error NU1903: advisory", false)]
    public void Source_availability_evidence_includes_tolerated_warnings(string output, bool unavailable)
    {
        // Arrange
        var process = new ProcessOutput(1, output);

        // Act
        var step = NuGetTrialStep.Read("candidateRestore", Path.GetTempPath(), process);

        // Assert
        step.SourceUnavailable.ShouldBe(unavailable);
    }
}
