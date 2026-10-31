using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PandaBear.Mediator.EntityFrameworkCore;
using PandaBear.Results;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Consumer-style subclass of the result transaction behavior used by the tests.
/// </summary>
internal sealed class ResultTransactionTestBehavior(DbContext dbContext, IServiceScopeFactory scopeFactory, ILogger logger)
    : ResultTransactionBehavior<ResultTransactionTestCommand, Result>(dbContext, scopeFactory, logger);
