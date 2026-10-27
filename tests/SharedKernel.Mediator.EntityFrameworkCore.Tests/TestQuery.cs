namespace PandaBear.Mediator.EntityFrameworkCore.Tests;

/// <summary>A test query that returns an integer, used to show queries skip transactions.</summary>
internal sealed record TestQuery : IQuery<int>;
