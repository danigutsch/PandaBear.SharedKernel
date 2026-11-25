using System.Text.Json.Nodes;

namespace SharedKernel.Repositories.Tests;

/// <summary>Representative NuGet assets documents for comparison tests.</summary>
internal static class NuGetRestoreGraphTestAssets
{
    /// <summary>A graph with an effective central override.</summary>
    internal static string Pinned => Unpinned.Replace(
        """
        "version": 4,
        """,
        """
        "version": 4,
        "centralTransitiveDependencyGroups": {
            "net11.0": { "Provider.Core": { "version": "[2.0.0, )" } }
        },
        """,
        StringComparison.Ordinal);

    /// <summary>The same resolved versions without an effective central override.</summary>
    internal const string Unpinned = """
        {
            "version": 4,
            "project": { "frameworks": { "net11.0": {
                "dependencies": { "Provider": { "target": "Package" } }
            } } },
            "targets": {
                "net11.0": {
                    "Provider/2.0.0": { "type": "package", "dependencies": { "Provider.Core": "2.0.0" } },
                    "Provider.Core/2.0.0": { "type": "package" }
                }
            },
            "libraries": {
                "Provider/2.0.0": { "sha512": "provider-hash-v2" },
                "Provider.Core/2.0.0": { "sha512": "core-hash-v2" }
            }
        }
        """;

    /// <summary>A provider upgrade with a native asset in an additional target.</summary>
    internal const string Upgraded = """
        {
            "version": 4,
            "project": { "frameworks": { "net11.0": {
                "dependencies": { "Provider": { "target": "Package" } }
            } } },
            "targets": {
                "net11.0/linux-x64": {
                    "Provider.Core/3.0.0": {
                        "type": "package",
                        "native": { "runtimes/linux-x64/native/libprovider.so": {} }
                    }
                },
                "net11.0": {
                    "Provider.Core/2.0.0": { "type": "package" },
                    "Provider/3.0.0": { "type": "package", "dependencies": { "Provider.Core": "3.0.0" } }
                }
            },
            "libraries": {
                "Provider/3.0.0": { "sha512": "provider-hash-v3" },
                "Provider.Core/3.0.0": { "sha512": "core-hash-v3" },
                "Provider.Core/2.0.0": { "sha512": "core-hash-v2" }
            }
        }
        """;

    /// <summary>Writes a complete, intentionally lock-exempt restore and an evaluated input for it.</summary>
    internal static void WriteLockExemptProject(TemporaryRepoConfigWorkspace workspace, string restored, bool stale = false)
    {
        var projectPath = Path.Combine(workspace.RootPath, "Provider.csproj");
        var outputPath = Path.Combine(workspace.RootPath, "obj");
        workspace.WriteFile("Provider.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var assets = JsonNode.Parse(restored)!.AsObject();
        var project = assets["project"]!.AsObject();
        project["restore"] = new JsonObject
        {
            ["projectPath"] = projectPath,
            ["outputPath"] = outputPath,
            ["restoreLockProperties"] = new JsonObject { ["restorePackagesWithLockFile"] = "false" },
        };
        workspace.WriteFile("obj/project.assets.json", assets.ToJsonString());
        var cache = new JsonObject { ["success"] = true, ["projectFilePath"] = projectPath };
        workspace.WriteFile("obj/project.nuget.cache", cache.ToJsonString());
        var evaluated = project.DeepClone().AsObject();
        if (stale)
        {
            evaluated["version"] = "3.0.0";
        }

        var spec = new JsonObject { ["projects"] = new JsonObject { [projectPath] = evaluated } };
        workspace.WriteFile("evaluated.json", spec.ToJsonString());
    }
}
