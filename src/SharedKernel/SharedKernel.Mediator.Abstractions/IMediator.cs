namespace SharedKernel.Mediator;

/// <summary>
/// Combines request sending and notification publishing.
/// </summary>
public interface IMediator : ISender, IPublisher;
