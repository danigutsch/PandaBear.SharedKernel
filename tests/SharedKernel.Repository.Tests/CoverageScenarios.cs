using System.Xml.Linq;

namespace PandaBear.Repository.Tests;

/// <summary>Writes the report classes and applies the diff, report, and symbol scenarios of the coverage gate tests.</summary>
internal static class CoverageScenarios
{
    public static string Class(string filename, params (int Number, bool Hit)[] lines) =>
        new XElement("class", new XAttribute("filename", filename), new XElement("lines", lines.Select(static line =>
            new XElement("line", new XAttribute("number", line.Number), new XAttribute("hits", line.Hit ? 1 : 0))))).ToString();

    public static void Apply(CoverageTestScope scope, string scenario)
    {
        switch (scenario)
        {
            case "mixed-symbols":
                scope.WriteSymbols("interface-symbols");
                File.Copy(Path.Combine(scope.Root, "src/bin/Release/net10.0/Core.pdb"), Path.Combine(scope.Root, "src/bin/Release/net10.0/Other.pdb"));
                scope.WriteSymbols("executable-symbols");
                break;
            case "unsupported-hash":
            case "absolute-symbols":
            case "interface-symbols":
            case "stale-symbols":
            case "executable-symbols":
                scope.WriteSymbols(scenario);
                break;
            case "space-path":
                scope.WriteFile("src/A B.cs", "uncovered\n");
                scope.Patch = "--- /dev/null\n+++ b/src/A B.cs\t\n@@ -0,0 +1 @@\n+uncovered\n";
                scope.WriteFile("coverage.xml", "<coverage><packages><package><classes><class filename='src/A B.cs'><lines><line number='1' hits='0' /></lines></class></classes></package></packages></coverage>");
                break;
            case "increment":
                scope.Patch = "--- /dev/null\n+++ b/src/Core.cs\n@@ -0,0 +1 @@\n+++ count;\n";
                break;
            case "duplicate":
                var report = XDocument.Parse(scope.ReadFile("coverage.xml"));
                var classes = report.Descendants("classes").Single();
                var duplicate = new XElement(classes.Elements("class").Single());
                foreach (var line in duplicate.Descendants("line").Where(static line => (int?)line.Attribute("number") > 3).ToArray())
                {
                    line.Remove();
                }

                classes.Add(duplicate, new XElement(duplicate), new XElement(duplicate));
                scope.WriteFile("coverage.xml", report.ToString());
                break;
            case "rename":
                scope.Patch = "diff --git a/src/Old.cs b/src/Core.cs\nsimilarity index 90%\nrename from src/Old.cs\nrename to src/Core.cs\n--- a/src/Old.cs\n+++ b/src/Core.cs\n@@ -1 +1 @@\n-old\n+new\n";
                break;
            case "delete":
                scope.Patch = "--- a/src/Core.cs\n+++ /dev/null\n@@ -1,5 +0,0 @@\n-one\n-two\n-three\n-four\n-five\n";
                break;
            case "non-executable":
                scope.Patch = "--- a/src/Core.cs\n+++ b/src/Core.cs\n@@ -6 +6 @@\n-// old\n+// new\n";
                break;
            case "excluded":
                scope.Patch = "--- /dev/null\n+++ b/src/Generated.g.cs\n@@ -0,0 +1 @@\n+generated\n";
                break;
            case "tests-only":
                scope.Patch = "--- /dev/null\n+++ b/tests/CoreTests.cs\n@@ -0,0 +1 @@\n+test\n";
                break;
            case "malformed-hunk":
                scope.Patch = "--- /dev/null\n+++ b/src/Core.cs\n@@ bad @@\n+one\n";
                break;
            case "truncated-hunk":
                scope.Patch = "--- /dev/null\n+++ b/src/Core.cs\n@@ -0,0 +1,5 @@\n+one\n";
                break;
            case "invalid-start":
                scope.Patch = "--- /dev/null\n+++ b/src/Core.cs\n@@ -0,0 +0,1 @@\n+one\n";
                break;
            case "header-without-hunk":
                scope.Patch = "--- /dev/null\n+++ b/src/Core.cs\n";
                break;
            case "quoted-path":
                scope.Patch = "--- /dev/null\n+++ \"b/src/A\\\"B.cs\"\n@@ -0,0 +1 @@\n+one\n";
                break;
            case "not-a-diff":
                scope.Patch = "unexpected output\n";
                break;
            case "failed-diff":
                scope.Patch = null;
                break;
            case "doctype":
                // Every changed line is covered, so only the refused document type declaration fails the check.
                scope.WriteFile("coverage.xml", "<!DOCTYPE coverage SYSTEM 'http://cobertura.sourceforge.net/xml/coverage-04.dtd'>\n"
                    + CoverageTestScope.Report("src/Core.cs", Enumerable.Range(1, 5).Select(static number => (number, true, (string?)null))));
                break;
            case "internal-entity":
                scope.WriteFile("coverage.xml", "<!DOCTYPE coverage [<!ENTITY value 'hidden'>]><coverage>&value;</coverage>");
                break;
            case "dtd":
                scope.WriteFile("coverage.xml", "<!DOCTYPE coverage [<!ENTITY secret SYSTEM 'file:///etc/passwd'>]><coverage>&secret;</coverage>");
                break;
            default:
                throw new ArgumentException("Unknown coverage scenario.", nameof(scenario));
        }
    }
}
