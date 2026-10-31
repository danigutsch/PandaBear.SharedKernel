using PandaBear.Results;

namespace PandaBear.Mediator.EntityFrameworkCore.Tests;

/// <summary>Command used by the result transaction behavior tests.</summary>
internal sealed record ResultTransactionCommand : ICommand<Result>;
