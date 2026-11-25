namespace SharedKernel.CI;

/// <summary>The consumer policy in <c>.config/sharedkernel-ci.json</c>.</summary>
/// <param name="Version">Format version; only <c>1</c> is supported.</param>
/// <param name="Solution">The repository-relative <c>.slnx</c> whose projects are selected.</param>
/// <param name="Selection">The path rules that decide what a change selects.</param>
/// <param name="Slices">The CI test slices, in matrix order.</param>
/// <param name="DefaultSlice">The slice that runs the solution test projects that no slice lists.</param>
/// <param name="OpenApi">The OpenAPI targets that <c>openapi</c> and the slices generate.</param>
/// <param name="Coverage">The changed-code coverage policy of the <c>coverage</c> commands.</param>
/// <param name="Mutation">The Stryker.NET runs of <c>run-mutation-tests</c>.</param>
public sealed record CiConfiguration(
    int Version,
    string Solution,
    SelectionConfiguration Selection,
    IReadOnlyList<SliceConfiguration> Slices,
    string? DefaultSlice = null,
    IReadOnlyList<OpenApiTargetConfiguration>? OpenApi = null,
    CoverageConfiguration? Coverage = null,
    MutationConfiguration? Mutation = null)
{
    /// <summary>Gets the optional projects required in every partial quality build.</summary>
    public QualityBuildConfiguration? QualityBuild { get; init; }
}
