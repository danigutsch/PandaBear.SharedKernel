namespace SharedKernel.Mediator;

/// <summary>
/// Represents a command that returns <see cref="Unit" />.
/// </summary>
public interface ICommand : IRequest<Unit>;
