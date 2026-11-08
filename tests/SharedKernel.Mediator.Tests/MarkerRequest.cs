namespace PandaBear.Mediator.Tests;

/// <summary>A request answered with the value held by the scope's <see cref="ScopeMarker"/>.</summary>
internal sealed record MarkerRequest : IRequest<string?>;
