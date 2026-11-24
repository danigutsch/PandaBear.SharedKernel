namespace PandaBear.Documentation.Tests;

/// <summary>Creates an isolated conformance check for the two Catalog initialization call shapes.</summary>
internal static class InvocationReceiverConformanceFixture
{
    /// <summary>The fixture's documentation conformance configuration path.</summary>
    public const string ConfigurationPath = "docs/architecture/documentation-conformance.json";

    /// <summary>The distinct Catalog initializer calls inside the application migration worker.</summary>
    public const string Initializers = """
        await PostgreSqlEventSourcingSchema.Initialize(catalogDataSource, options: null, ct);
        if (environment.IsDevelopment())
        {
            await CatalogGalleryProcessStatusIndexes.Initialize(catalogDataSource, ct);
        }
        """;

    /// <summary>Requirements for the two receivers using their original argument lists.</summary>
    public const string Requirements = """
        {
          "sourcePath": "src/DatabaseInitializationWorker.cs",
          "methodName": "RunMigrations",
          "parameterCount": 2,
          "invokedMethodName": "Initialize",
          "receiver": "PostgreSqlEventSourcingSchema",
          "expectedCount": 1,
          "expectedArguments": ["catalogDataSource", "options: null", "ct"]
        },
        {
          "sourcePath": "src/DatabaseInitializationWorker.cs",
          "methodName": "RunMigrations",
          "parameterCount": 2,
          "invokedMethodName": "Initialize",
          "receiver": "CatalogGalleryProcessStatusIndexes",
          "expectedCount": 1,
          "expectedArguments": ["catalogDataSource", "ct"]
        }
        """;

    /// <summary>Writes the caller and invocation requirements to a temporary repository.</summary>
    /// <param name="calls">The statements to check.</param>
    /// <param name="requirements">The individual invocation requirements as JSON.</param>
    /// <param name="topLevel">Whether to put the statements in the top-level rather than the migration method.</param>
    /// <returns>The isolated conformance workspace.</returns>
    public static TemporaryDocumentationWorkspace Create(string calls, string requirements = Requirements, bool topLevel = false)
    {
        var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("docs/FLOWS.md",
            "<!-- doc-fact:initialization:start -->\n- current-runtime: `stable`\n<!-- doc-fact:initialization:end -->");
        workspace.WriteFile(ConfigurationPath, $$"""
            {
              "checks": [{
                "name": "catalog-runtime",
                "documentPath": "docs/FLOWS.md",
                "markerName": "initialization",
                "factName": "current-runtime",
                "expectedIdentifiers": ["stable"],
                "invocationRequirements": [{{requirements}}]
              }]
            }
            """);
        workspace.WriteFile("src/DatabaseInitializationWorker.cs", topLevel ? calls : $$"""
            class DatabaseInitializationWorker
            {
                private static async Task RunMigrations(IServiceProvider serviceProvider, CancellationToken ct)
                {
                    {{calls}}
                }
            }
            """);
        return workspace;
    }
}
