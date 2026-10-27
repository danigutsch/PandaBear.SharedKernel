namespace PandaBear.Mediator.EntityFrameworkCore.Tests;

/// <summary>A test command that returns an integer and is handled against TestDbContext.</summary>
internal sealed record TestCommand : ICommand<int>;
