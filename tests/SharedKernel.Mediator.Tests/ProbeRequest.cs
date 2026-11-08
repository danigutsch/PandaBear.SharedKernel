namespace PandaBear.Mediator.Tests;

/// <summary>A request answered with the id of the scope that handled it.</summary>
internal sealed record ProbeRequest : IRequest<Guid>;
