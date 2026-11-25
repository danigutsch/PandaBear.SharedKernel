namespace SharedKernel.Mediator;

/// <summary>
/// Represents the next step in a generated pipeline chain.
/// </summary>
/// <typeparam name="TResponse">The response type produced by the next step.</typeparam>
/// <returns>The response produced by the next step.</returns>
public delegate ValueTask<TResponse> RequestHandlerContinuation<TResponse>();
