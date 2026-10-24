namespace PandaBear.CI.Tool;

/// <summary>The consumer policy in <c>.config/sharedkernel-ci.json</c>.</summary>
/// <param name="Version">Format version; only <c>1</c> is supported.</param>
/// <param name="Solution">The repository-relative <c>.slnx</c> whose projects are selected.</param>
/// <param name="Selection">The path rules that decide what a change selects.</param>
/// <param name="Slices">The CI test slices, in matrix order.</param>
/// <param name="DefaultSlice">The slice that runs the solution test projects that no slice lists.</param>
/// <param name="OpenApi">The OpenAPI targets that <c>openapi</c> and the slices generate.</param>
internal sealed record CiConfiguration(
    int Version,
    string Solution,
    SelectionConfiguration Selection,
    SliceConfiguration[] Slices,
    string? DefaultSlice = null,
    OpenApiTargetConfiguration[]? OpenApi = null);
