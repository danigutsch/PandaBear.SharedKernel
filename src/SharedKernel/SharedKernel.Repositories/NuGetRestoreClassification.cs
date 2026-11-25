namespace SharedKernel.Repositories;

/// <summary>How the package enters the evaluated project framework.</summary>
internal enum NuGetRestoreClassification
{
    /// <summary>An evaluated package reference.</summary>
    Direct,

    /// <summary>A dependency promoted by a central transitive pin.</summary>
    CentralTransitive,

    /// <summary>A dependency reached through another reference.</summary>
    Transitive,
}
