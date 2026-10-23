namespace PandaBear.Versioning.Tests;

internal static class RepositoryRoot
{
    public static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SharedKernel.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("SharedKernel.slnx was not found above " + AppContext.BaseDirectory);
    }
}
