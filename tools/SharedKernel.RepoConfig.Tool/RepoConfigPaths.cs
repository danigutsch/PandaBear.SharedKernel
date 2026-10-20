namespace SharedKernel.RepoConfig.Tool;

internal static class RepoConfigPaths
{
    public const string Roadmap = "roadmap";
    public const string Items = "roadmap/items";
    public const string Reconciliation = "roadmap/reconciliation";
    public const string Schema = "roadmap/schema";
    public const string Themes = "roadmap/themes";
    public const string RoadmapReadme = "roadmap/README.md";
    public const string Config = "roadmap/config.json";
    public const string Order = "roadmap/order.json";
    public const string ConfigSchema = "roadmap/schema/roadmap-config.schema.json";
    public const string ItemSchema = "roadmap/schema/roadmap-item.schema.json";
    public const string DefaultTheme = "roadmap/themes/repo-operations.json";
    public const string DefaultItem = "roadmap/items/RM-001-roadmap-gitops.json";

    public static readonly string[] RequiredDirectories =
    [
        Roadmap,
        Items,
        Schema,
        Themes
    ];

    public static readonly string[] RequiredFiles =
    [
        RoadmapReadme,
        Config,
        Order,
        ConfigSchema,
        ItemSchema
    ];

    public static string RelativeTo(string rootPath, string fullPath) =>
        Normalize(Path.GetRelativePath(rootPath, fullPath));

    public static string Normalize(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
}
