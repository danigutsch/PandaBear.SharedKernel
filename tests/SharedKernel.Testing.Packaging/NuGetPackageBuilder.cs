using System.IO.Compression;

namespace PandaBear.Testing.Packaging;

/// <summary>
/// Builds lightweight NuGet package files for packaging tests.
/// </summary>
public static class NuGetPackageBuilder
{
    private const string ToolPackageType = """

            <packageTypes>
              <packageType name="DotnetTool" />
            </packageTypes>
        """;

    /// <summary>
    /// Writes a package with a generated nuspec file.
    /// </summary>
    /// <param name="directory">Directory that receives the package.</param>
    /// <param name="packageId">Package identifier written to the nuspec.</param>
    /// <param name="version">Package version written to the nuspec and file name.</param>
    /// <param name="dependencies">Package dependencies written to the nuspec.</param>
    /// <returns>The created package path.</returns>
    public static string WritePackage(
        string directory,
        string packageId,
        string version,
        params (string Id, string Version)[] dependencies)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, packageId + "." + version + ".nupkg");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = archive.CreateEntry(packageId + ".nuspec");
        using var writer = new StreamWriter(entry.Open());
        writer.Write(CreateNuspec(packageId, version, dependencies, string.Empty));
        return path;
    }

    /// <summary>
    /// Writes a .NET tool package with a generated nuspec file that declares the <c>DotnetTool</c> package type.
    /// </summary>
    /// <param name="directory">Directory that receives the package.</param>
    /// <param name="packageId">Package identifier written to the nuspec.</param>
    /// <param name="version">Package version written to the nuspec and file name.</param>
    /// <param name="entries">Other package entries by path, such as <c>tools/net10.0/any/DotnetToolSettings.xml</c>.</param>
    /// <returns>The created package path.</returns>
    public static string WriteToolPackage(
        string directory,
        string packageId,
        string version,
        params (string Path, string Content)[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, packageId + "." + version + ".nupkg");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, packageId + ".nuspec", CreateNuspec(packageId, version, [], ToolPackageType));
        foreach (var (entryPath, content) in entries)
        {
            WriteEntry(archive, entryPath, content);
        }

        return path;
    }

    /// <summary>
    /// Writes a package archive without a nuspec file.
    /// </summary>
    /// <param name="directory">Directory that receives the package.</param>
    /// <param name="fileName">Package file name.</param>
    /// <returns>The created package path.</returns>
    public static string WritePackageWithoutNuspec(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        archive.CreateEntry("placeholder.txt");

        return path;
    }

    private static void WriteEntry(ZipArchive archive, string entryPath, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(entryPath).Open());
        writer.Write(content);
    }

    private static string CreateNuspec(string packageId, string version, (string Id, string Version)[] dependencies, string packageTypes)
    {
        var dependencyXml = string.Join(
            Environment.NewLine,
            dependencies.Select(dependency => "        <dependency id=\"" + dependency.Id + "\" version=\"" + dependency.Version + "\" />"));
        return """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>
            """ + packageId + """
            </id>
                <version>
            """ + version + """
            </version>
            """ + packageTypes + """

                <dependencies>
            """ + Environment.NewLine + dependencyXml + Environment.NewLine + """
                </dependencies>
              </metadata>
            </package>
            """;
    }
}
