using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using SharedKernel.Processes;

namespace SharedKernel.CodeFixes.MSBuild;

/// <summary>Queries the target SDK for the build contexts that compile a new source file.</summary>
internal static class CompileMembership
{
    private const string CompileProperties = "Configuration,Configurations,Platform,Platforms,TargetFramework,TargetFrameworks";
    private static readonly BoundedProcessRunner ProcessRunner = new();

    /// <summary>Returns a new compile item when some evaluated contexts need one.</summary>
    public static async Task<XElement?> FindMissingItem(string projectPath, string sourcePath, XNamespace projectNamespace, CancellationToken ct)
    {
        var contexts = await Evaluate(projectPath, sourcePath, ct).ConfigureAwait(false);
        var missing = contexts.Where(static context => context.Count == 0).ToArray();
        if (missing.Length == 0)
        {
            return null;
        }

        var include = Path.GetRelativePath(Path.GetDirectoryName(projectPath)!, sourcePath);
        var item = new XElement(projectNamespace + "Compile", new XAttribute("Include", Escape(include)));
        if (missing.Length != contexts.Count)
        {
            item.SetAttributeValue("Condition", string.Join(" Or ", missing.Select(static context => context.Condition)));
        }

        return new XElement(projectNamespace + "ItemGroup", item);
    }

    /// <summary>Verifies that every evaluated context compiles the helper exactly once.</summary>
    /// <exception cref="InvalidOperationException">At least one evaluated context omits the file.</exception>
    public static async Task Verify(string projectPath, string sourcePath, CancellationToken ct)
    {
        if ((await Evaluate(projectPath, sourcePath, ct).ConfigureAwait(false)).Any(static context => context.Count != 1))
        {
            throw new InvalidOperationException($"The new source file is not compiled in every evaluated build context: {sourcePath}");
        }
    }

    private static async Task<IReadOnlyList<Context>> Evaluate(string projectPath, string sourcePath, CancellationToken ct)
    {
        var dotnet = PathExecutableResolver.Default.Resolve("dotnet")
            ?? throw new InvalidOperationException("Could not find dotnet to evaluate the new source file.");
        using var defaults = await Query(dotnet, projectPath, configuration: null, platform: null, framework: null, includeItems: false, ct).ConfigureAwait(false);
        var properties = defaults.RootElement.GetProperty("Properties");
        var configurations = Values(properties, "Configurations", "Configuration");
        var platforms = Values(properties, "Platforms", "Platform");
        var contexts = new List<Context>();
        foreach (var configuration in configurations)
        {
            foreach (var platform in platforms)
            {
                using var outer = await Query(dotnet, projectPath, configuration, platform, framework: null, includeItems: true, ct).ConfigureAwait(false);
                string[] frameworks = Split(Property(outer.RootElement.GetProperty("Properties"), "TargetFrameworks"));
                if (frameworks.Length == 0)
                {
                    contexts.Add(new(configuration, platform, Framework: null, Count(outer.RootElement, sourcePath)));
                    continue;
                }

                foreach (var framework in frameworks)
                {
                    using var inner = await Query(dotnet, projectPath, configuration, platform, framework, includeItems: true, ct).ConfigureAwait(false);
                    contexts.Add(new(configuration, platform, framework, Count(inner.RootElement, sourcePath)));
                }
            }
        }

        return contexts;
    }

    private static async Task<JsonDocument> Query(
        string dotnet, string projectPath, string? configuration, string? platform, string? framework, bool includeItems, CancellationToken ct)
    {
        var start = new ProcessStartInfo(dotnet) { WorkingDirectory = Path.GetDirectoryName(projectPath)! };
        // The SDK must write only the requested MSBuild JSON, even when CI enables CLI diagnostics.
        start.Environment["DOTNET_CLI_CONTEXT_VERBOSE"] = "false";
        start.Environment["DOTNET_NOLOGO"] = "true";
        start.ArgumentList.Add("msbuild");
        start.ArgumentList.Add(projectPath);
        start.ArgumentList.Add("-nologo");
        start.ArgumentList.Add("-nodeReuse:false");
        start.ArgumentList.Add("-maxCpuCount:1");
        start.ArgumentList.Add("-getProperty:" + CompileProperties);
        if (includeItems)
        {
            start.ArgumentList.Add("-getItem:Compile");
        }

        AddProperty(start, "Configuration", configuration);
        AddProperty(start, "Platform", platform);
        AddProperty(start, "TargetFramework", framework);
        var result = await ProcessRunner.Run(start, forward: null, ct).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"MSBuild could not evaluate compile items in {projectPath}.{Environment.NewLine}{result.StandardError}{result.StandardOutput}");
        }

        return JsonDocument.Parse(result.StandardOutput);
    }

    private static void AddProperty(ProcessStartInfo start, string property, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            start.ArgumentList.Add($"-property:{property}={Escape(value)}");
        }
    }

    private static int Count(JsonElement root, string sourcePath)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var count = root.GetProperty("Items").GetProperty("Compile").EnumerateArray()
            .Count(item => string.Equals(item.GetProperty("FullPath").GetString(), sourcePath, comparison));
        return count <= 1 ? count : throw new InvalidOperationException($"MSBuild evaluates the new source file more than once: {sourcePath}");
    }

    private static string[] Values(JsonElement properties, string declared, string current) =>
        [.. Split(Property(properties, declared)).Append(Property(properties, current)).Distinct(StringComparer.OrdinalIgnoreCase)];

    private static string[] Split(string values) =>
        values.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Property(JsonElement properties, string key) => properties.GetProperty(key).GetString() ?? string.Empty;

    private static string Escape(string value) => value
        .Replace("%", "%25", StringComparison.Ordinal)
        .Replace("$", "%24", StringComparison.Ordinal)
        .Replace("@", "%40", StringComparison.Ordinal)
        .Replace("'", "%27", StringComparison.Ordinal)
        .Replace("(", "%28", StringComparison.Ordinal)
        .Replace(")", "%29", StringComparison.Ordinal)
        .Replace(";", "%3b", StringComparison.Ordinal)
        .Replace(",", "%2c", StringComparison.Ordinal)
        .Replace("?", "%3f", StringComparison.Ordinal)
        .Replace("*", "%2a", StringComparison.Ordinal);

    private sealed record Context(string Configuration, string Platform, string? Framework, int Count)
    {
        public string Condition =>
            $"('$(Configuration)' == '{Escape(Configuration)}' And '$(Platform)' == '{Escape(Platform)}'"
            + (Framework is null ? ")" : $" And '$(TargetFramework)' == '{Escape(Framework)}')");
    }
}
