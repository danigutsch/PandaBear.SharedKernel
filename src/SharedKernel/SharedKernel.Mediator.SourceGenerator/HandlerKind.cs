namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Identifies the semantic role of a request handler.
/// </summary>
internal enum HandlerKind
{
    Request,
    Command,
    CommandWithResponse,
    Query,
}
