using System.IO.Compression;

namespace SharedKernel.Versioning.Tests;

internal static class VersioningToolPackageTestHelper
{
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
        writer.Write(CreateNuspec(packageId, version, dependencies));
        return path;
    }

    public static string WritePackageWithoutNuspec(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        archive.CreateEntry("placeholder.txt");

        return path;
    }

    private static string CreateNuspec(string packageId, string version, (string Id, string Version)[] dependencies)
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
                <dependencies>
            """ + Environment.NewLine + dependencyXml + Environment.NewLine + """
                </dependencies>
              </metadata>
            </package>
            """;
    }
}
