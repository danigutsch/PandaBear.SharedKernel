namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Indicates whether a pipeline applies to one request contract or an open generic family.
/// </summary>
internal enum PipelineApplicability
{
    Closed,
    OpenGeneric,
}
