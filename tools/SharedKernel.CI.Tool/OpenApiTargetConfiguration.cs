namespace PandaBear.CI.Tool;

/// <summary>An OpenAPI target: the project whose build generates or refreshes OpenAPI documents when a property is set.</summary>
/// <param name="Name">Lowercase letters and digits in hyphen-separated words; <c>openapi --target</c> and <c>slices[].openApi</c> name it.</param>
/// <param name="Project">The repository-relative project that <c>dotnet build</c> builds.</param>
/// <param name="GenerateProperty">The MSBuild property set to <c>true</c> to generate the documents.</param>
/// <param name="RefreshProperty">The MSBuild property set to <c>true</c> to generate the documents and refresh the committed copies.</param>
internal sealed record OpenApiTargetConfiguration(string Name, string Project, string GenerateProperty, string RefreshProperty);
