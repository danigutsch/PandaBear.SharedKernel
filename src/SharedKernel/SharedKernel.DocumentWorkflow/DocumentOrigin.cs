namespace PandaBear.DocumentWorkflow;

/// <summary>Identifies the source and template used to generate one revision.</summary>
/// <param name="SourceId">The application identifier of the source.</param>
/// <param name="SourceVersion">The application's deterministic version of the source data.</param>
/// <param name="TemplateId">The stable template identifier.</param>
/// <param name="TemplateVersion">The version of the template used to generate the draft.</param>
public sealed record DocumentOrigin(Guid SourceId, string SourceVersion, string TemplateId, string TemplateVersion);
