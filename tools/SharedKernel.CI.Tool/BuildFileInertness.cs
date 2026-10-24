using System.Text;
using System.Xml;
using System.Xml.Linq;
using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>
/// Decides whether tracked build files are inert: they cannot read other repository files, so a documentation change
/// cannot change a build. An inert file has no <c>Exec</c>, <c>UsingTask</c>, or <c>ReadLinesFromFile</c> element and
/// no <c>System.IO.File</c> property function, and imports only <c>Sdk.props</c> and <c>Sdk.targets</c> of
/// <c>Microsoft.NET.Sdk</c>, or, in a <c>Directory.Build.props</c> or <c>Directory.Build.targets</c>, the file of the
/// same name above its directory through <c>GetPathOfFileAbove</c>.
/// </summary>
internal static class BuildFileInertness
{
    /// <summary>The Git pathspecs of the tracked build files.</summary>
    public static readonly string[] Pathspecs = ["*.csproj", "*.props", "*.targets"];

    private const int MaxBytes = 4 * 1024 * 1024;

    /// <summary>Returns why one of <paramref name="files"/> is not inert, or <see langword="null"/> when every file is.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="files">Repository-relative build file paths, as <c>git ls-files</c> lists them.</param>
    /// <param name="ct">Cancels the reads.</param>
    public static async Task<string?> FindNonInert(string root, IEnumerable<string> files, CancellationToken ct)
    {
        foreach (var file in files)
        {
            XDocument document;
            try
            {
                var content = Encoding.UTF8.GetString(await BoundedFile.Read(RepositoryPath.Resolve(root, file), MaxBytes, file, ct).ConfigureAwait(false));
                if (content.Contains("System.IO.File", StringComparison.Ordinal))
                {
                    return file + " calls System.IO.File.";
                }

                document = XDocument.Parse(content.TrimStart('\uFEFF'));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException or InvalidOperationException or InvalidDataException)
            {
                return file + " cannot be read as MSBuild XML.";
            }

            var name = Path.GetFileName(file);
            foreach (var element in document.Descendants())
            {
                if (element.Name.LocalName is "Exec" or "UsingTask" or "ReadLinesFromFile")
                {
                    return file + " uses " + element.Name.LocalName + ".";
                }

                if (element.Name.LocalName == "Import" && !IsInertImport(name, element))
                {
                    return file + " imports a file other than the SDK or the " + name + " above it.";
                }
            }
        }

        return null;
    }

    private static bool IsInertImport(string fileName, XElement import)
    {
        var project = (string?)import.Attribute("Project");
        if ((string?)import.Attribute("Sdk") is { } sdk)
        {
            return sdk == "Microsoft.NET.Sdk" && project is "Sdk.props" or "Sdk.targets";
        }

        return fileName is "Directory.Build.props" or "Directory.Build.targets"
            && (project == "$([MSBuild]::GetPathOfFileAbove('" + fileName + "', '$(MSBuildThisFileDirectory)..'))"
                || project == "$([MSBuild]::GetPathOfFileAbove('" + fileName + "', '$(MSBuildThisFileDirectory)../'))");
    }
}
