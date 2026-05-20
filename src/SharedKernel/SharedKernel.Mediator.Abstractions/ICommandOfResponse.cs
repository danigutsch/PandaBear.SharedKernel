using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Mediator;

/// <summary>
/// Represents a command that returns a response value.
/// </summary>
/// <typeparam name="TResponse">The response type returned for the command.</typeparam>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Marker command contract for generated mediator dispatch.")]
public interface ICommand<TResponse> : IRequest<TResponse>;
