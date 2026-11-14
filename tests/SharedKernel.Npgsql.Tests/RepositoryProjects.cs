namespace PandaBear.Npgsql.Tests;

/// <summary>Finds project files of this repository for application models that need a real project path.</summary>
internal static class RepositoryProjects
{
    /// <summary>Gets the full path of a small ASP.NET Core test application's project file.</summary>
    internal static string TestApp => Path.Combine(Root(), "tests", "SharedKernel.Testing.AspNetCore.TestApp", "SharedKernel.Testing.AspNetCore.TestApp.csproj");

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SharedKernel.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The repository root (SharedKernel.slnx) was not found above the test output.");
    }
}
