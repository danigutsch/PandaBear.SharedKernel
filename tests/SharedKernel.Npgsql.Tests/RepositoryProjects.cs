namespace SharedKernel.Npgsql.Tests;

/// <summary>Finds project files of this repository for application models that need a real project path.</summary>
internal static class RepositoryProjects
{
    /// <summary>Gets the full path of a small ASP.NET Core test application's project file.</summary>
    internal static string TestApp => Path.Combine(RepositoryRoot.Locate("SharedKernel.slnx"), "tests", "SharedKernel.Testing.AspNetCore.TestApp", "SharedKernel.Testing.AspNetCore.TestApp.csproj");
}
