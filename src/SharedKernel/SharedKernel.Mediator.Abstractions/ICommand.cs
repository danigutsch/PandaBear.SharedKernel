using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Mediator;

/// <summary>
/// Represents a command that returns <see cref="Unit" />.
/// </summary>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Marker command contract for generated mediator dispatch.")]
public interface ICommand : IRequest<Unit>;
