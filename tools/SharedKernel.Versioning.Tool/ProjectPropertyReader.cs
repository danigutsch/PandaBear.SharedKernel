using System.Xml;
using System.Xml.Linq;

namespace SharedKernel.Versioning.Tool;

internal static class ProjectPropertyReader
{
    public static string? Read(string project, string propertyName)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(project);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            throw new ArgumentException($"Invalid project property file: {project}", ex);
        }

        var value = document.Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, propertyName, StringComparison.Ordinal))
            ?.Value
            .Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
