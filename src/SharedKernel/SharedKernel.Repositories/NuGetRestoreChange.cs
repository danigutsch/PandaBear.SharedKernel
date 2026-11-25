namespace SharedKernel.Repositories;

/// <summary>A package difference between two evaluated restore graphs.</summary>
/// <param name="Target">The framework, with the RID suffix where applicable.</param>
/// <param name="Package">The package identifier.</param>
/// <param name="Before">The baseline package, if present.</param>
/// <param name="After">The candidate package, if present.</param>
internal sealed record NuGetRestoreChange(string Target, string Package, NuGetRestorePackage? Before, NuGetRestorePackage? After);
