namespace PandaBear.RepoConfig.Tool;

/// <summary>Relative paths of the roadmap configuration files and directories.</summary>
internal static class RepoConfigPaths
{
    /// <summary>Roadmap directory.</summary>
    public const string Roadmap = "roadmap";
    /// <summary>Directory of roadmap item files.</summary>
    public const string Items = "roadmap/items";
    /// <summary>Directory of reconciliation manifests.</summary>
    public const string Reconciliation = "roadmap/reconciliation";
    /// <summary>Directory of JSON schemas.</summary>
    public const string Schema = "roadmap/schema";
    /// <summary>Directory of roadmap themes.</summary>
    public const string Themes = "roadmap/themes";
    /// <summary>Roadmap README file.</summary>
    public const string RoadmapReadme = "roadmap/README.md";
    /// <summary>Roadmap configuration file.</summary>
    public const string Config = "roadmap/config.json";
    /// <summary>Roadmap order file.</summary>
    public const string Order = "roadmap/order.json";
    /// <summary>JSON schema of the configuration file.</summary>
    public const string ConfigSchema = "roadmap/schema/roadmap-config.schema.json";
    /// <summary>JSON schema of roadmap item files.</summary>
    public const string ItemSchema = "roadmap/schema/roadmap-item.schema.json";
    /// <summary>Theme file created by default.</summary>
    public const string DefaultTheme = "roadmap/themes/repo-operations.json";
    /// <summary>Item file created by default.</summary>
    public const string DefaultItem = "roadmap/items/RM-001-roadmap-gitops.json";

    /// <summary>Directories every initialized repository contains.</summary>
    public static readonly string[] RequiredDirectories =
    [
        Roadmap,
        Items,
        Schema,
        Themes
    ];

    /// <summary>Files every initialized repository contains.</summary>
    public static readonly string[] RequiredFiles =
    [
        RoadmapReadme,
        Config,
        Order,
        ConfigSchema,
        ItemSchema
    ];

    /// <summary>Returns the full path relative to the root, with forward slashes.</summary>
    /// <param name="rootPath">Root directory.</param>
    /// <param name="fullPath">Path below the root.</param>
    public static string RelativeTo(string rootPath, string fullPath) =>
        Normalize(Path.GetRelativePath(rootPath, fullPath));

    /// <summary>Returns the path with forward slashes as separators.</summary>
    /// <param name="path">Path to normalize.</param>
    public static string Normalize(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
}
