using System.Xml;
using System.Xml.Linq;

namespace SharedKernel.Versioning;

/// <summary>
/// Reads MSBuild properties from project-style XML files.
/// </summary>
public static class ProjectPropertyReader
{
    /// <summary>
    /// Reads a property value by name using case-insensitive MSBuild property matching.
    /// </summary>
    /// <param name="project">The project-style XML file.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The trimmed value, or <see langword="null" /> when missing or empty.</returns>
    /// <exception cref="ArgumentException">Thrown when the file cannot be read as XML.</exception>
    public static string? Read(string project, string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

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
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, propertyName, StringComparison.OrdinalIgnoreCase))
            ?.Value
            .Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
