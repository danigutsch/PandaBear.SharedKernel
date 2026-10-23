using System.Text.RegularExpressions;

namespace PandaBear.Versioning.Tests;

/// <summary>Deletes the package consumption evidence workspaces that an invocation names in its output or errors.</summary>
internal sealed partial class EvidenceWorkspaces(CommandLineInvocation invocation) : IDisposable
{
    public void Dispose()
    {
        var names = WorkspaceName().Matches(invocation.Output)
            .Concat(WorkspaceName().Matches(invocation.Error))
            .Select(static match => match.Value)
            .Distinct(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var path = Path.Combine(Path.GetTempPath(), name);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    [GeneratedRegex("sharedkernel-package-check-[0-9a-f]{32}", RegexOptions.CultureInvariant)]
    private static partial Regex WorkspaceName();
}
