namespace PandaBear.Mediator.EntityFrameworkCore.Tests;

/// <summary>A test command that returns an integer and is handled against OtherTestDbContext.</summary>
internal sealed record OtherTestCommand : ICommand<int>;
