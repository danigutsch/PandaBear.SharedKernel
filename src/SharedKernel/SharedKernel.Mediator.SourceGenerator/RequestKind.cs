namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Identifies the semantic role of a discovered request.
/// </summary>
internal enum RequestKind
{
    Request,
    Command,
    CommandWithResponse,
    Query,
}
