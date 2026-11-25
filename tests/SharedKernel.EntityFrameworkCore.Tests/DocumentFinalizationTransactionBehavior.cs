using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PandaBear.DocumentWorkflow.EntityFrameworkCore;
using PandaBear.Mediator;
using PandaBear.Mediator.EntityFrameworkCore;
using PandaBear.Results;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Makes the test host own the result-aware finalization transaction.</summary>
/// <param name="context">The document and audit context.</param>
/// <param name="scopeFactory">The factory for outcome hook scopes.</param>
[PipelineOrder(PipelineStage.Transaction)]
internal sealed class DocumentFinalizationTransactionBehavior(DocumentWorkflowTestContext context, IServiceScopeFactory scopeFactory)
    : ResultTransactionBehavior<FinalizeDocumentRevisionCommand, Result<DocumentFinalizationReceipt>>(context, scopeFactory, NullLogger.Instance);
