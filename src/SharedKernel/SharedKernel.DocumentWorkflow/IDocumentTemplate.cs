namespace SharedKernel.DocumentWorkflow;

/// <summary>Projects application-owned source data to structured document content.</summary>
/// <typeparam name="TSource">The application-owned source model.</typeparam>
public interface IDocumentTemplate<in TSource>
    where TSource : notnull
{
    /// <summary>Gets the stable identifier of the template.</summary>
    string TemplateId { get; }

    /// <summary>Gets the version of the template used by this instance.</summary>
    string TemplateVersion { get; }

    /// <summary>Builds structured content from the source snapshot.</summary>
    /// <param name="source">The application data to render.</param>
    /// <returns>The resolved fields and captured branding.</returns>
    DocumentContent Create(TSource source);
}
