using System.Xml.Linq;

namespace SharedKernel.Versioning.Tool;

internal static class ProjectPropertyReader
{
    public static string? Read(string project, string propertyName)
    {
        var document = XDocument.Load(project);
        var value = document.Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, propertyName, StringComparison.Ordinal))
            ?.Value
            .Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
