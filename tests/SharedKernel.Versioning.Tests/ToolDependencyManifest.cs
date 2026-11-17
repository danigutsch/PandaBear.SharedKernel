using System.Text.Json.Nodes;

namespace PandaBear.Versioning.Tests;

/// <summary>Writes the dependency manifest (<c>.deps.json</c>) that the build puts beside a framework-dependent tool's entry point.</summary>
internal static class ToolDependencyManifest
{
    /// <summary>Path of the probe tool's dependency manifest inside a net10.0 .NET tool package, beside <see cref="DotnetToolSettings.EntryPath" />.</summary>
    public const string EntryPath = "tools/net10.0/any/Probe.Tool.deps.json";

    /// <summary>Returns the path of the probe tool's dependency manifest for <paramref name="framework" />, such as <c>net11.0</c>.</summary>
    public static string EntryPathFor(string framework) => $"tools/{framework}/any/Probe.Tool.deps.json";

    /// <summary>
    /// Returns a dependency manifest whose libraries are the probe tool's own project and each of <paramref name="packages" />,
    /// written as <c>Id/Version</c>, as a NuGet package that the tool carries.
    /// </summary>
    public static string Listing(params string[] packages)
    {
        var libraries = new JsonObject { ["Probe.Tool/1.0.0"] = new JsonObject { ["type"] = "project", ["serviceable"] = false, ["sha512"] = string.Empty } };
        foreach (var package in packages)
        {
            libraries[package] = new JsonObject { ["type"] = "package", ["serviceable"] = true, ["sha512"] = "sha512-test" };
        }

        return new JsonObject { ["runtimeTarget"] = new JsonObject { ["name"] = ".NETCoreApp,Version=v10.0" }, ["libraries"] = libraries }.ToJsonString();
    }
}
