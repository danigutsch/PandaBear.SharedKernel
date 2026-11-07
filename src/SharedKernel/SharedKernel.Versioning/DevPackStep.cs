namespace PandaBear.Versioning;

/// <summary>One project to consider when packing SharedKernel for local development.</summary>
/// <param name="Name">The project name, for example <c>SharedKernel.Results</c>.</param>
/// <param name="ProjectPath">The full path of the project file.</param>
/// <param name="PackageId">The NuGet package id the project produces.</param>
/// <param name="Rebuild"><see langword="true"/> when the package must be packed now; <see langword="false"/> when the feed already holds it.</param>
/// <param name="Stale"><see langword="true"/> when the package is kept but a source file of the project changed after the package was packed, so the feed copy is out of date; never <see langword="true"/> for a rebuilt step.</param>
public sealed record DevPackStep(string Name, string ProjectPath, string PackageId, bool Rebuild, bool Stale = false);
