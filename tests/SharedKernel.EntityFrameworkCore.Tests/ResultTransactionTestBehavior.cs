using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Mediator;
using SharedKernel.Mediator.EntityFrameworkCore;
using SharedKernel.Results;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>
/// Consumer-style subclass of the result transaction behavior used by the tests.
/// </summary>
/// <typeparam name="TCommand">The command handled by the behavior.</typeparam>
/// <param name="dbContext">The context that owns the transaction boundary.</param>
/// <param name="scopeFactory">Creates the fresh scope the outcome hooks run in.</param>
/// <param name="logger">Logs hook failures.</param>
#pragma warning disable SKMED020 // The direct transaction fixture fixes the response type to Result.
internal sealed class ResultTransactionTestBehavior<TCommand>(DbContext dbContext, IServiceScopeFactory scopeFactory, ILogger logger)
    : ResultTransactionBehavior<TCommand, Result>(dbContext, scopeFactory, logger)
    where TCommand : ICommand<Result>;
#pragma warning restore SKMED020
