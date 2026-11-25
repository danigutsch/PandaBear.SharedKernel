# SharedKernel.DocumentWorkflow.EntityFrameworkCore

Persists validated workflow revisions in an EF Core context. Register
`AddDocumentWorkflowStorage<TContext>()` and the database provider's
`IUniqueViolationParser`. Apply the context's model configurations and generate
a consumer migration for the `document_workflow.revisions` table.

`Create` saves the initial draft and returns a storage version. `Load` returns a
validated revision with that version. Pass the version to `Save` when applying a
lifecycle transition, or to `Replace` when saving both the preceding historical
snapshot and a new revision. Replacement writes both rows in one save.

The row version is the concurrency token. A stale version, concurrent write, or
duplicate revision identity returns a conflict with the `ConcurrencyConflict`
error code. Dispose the context after a failed save and reload before retrying.
The store preserves committed review evidence and the original finalized
artifact handle across writes.

The payload column has an explicit version. The reader validates the supported
shape through the workflow restore boundary and returns an invalid result for
unknown versions or inconsistent data. Artifact handles live in a separate
column. The host provides artifact bytes, access control, retention, and holds.

This package uses EF Core model binding and opts out of NativeAOT compatibility.
The core document workflow library keeps its separate deployment contract.

## Mediated finalization

Register `AddDocumentFinalizationCommand<TContext, TSource>()` along with
`AddIdempotencyStore<TContext>()`, a host-configured `DocumentFinalizer<TSource>`,
and an `IAuditTrailSink<DocumentFinalizationAuditEntry>` backed by the same unit of work.
Supply `TimeProvider` to override the system clock. The handler resolves the
idempotency store keyed to the document context, including in hosts with
multiple EF contexts.

In the host's generated mediator module, define an `ICommand<Result<DocumentFinalizationReceipt>>`
that carries a `DocumentFinalizationRequest`. Its handler forwards the request to
the injected `FinalizeDocumentRevisionHandler<TContext, TSource>`. Apply the
[Result transaction behavior](../SharedKernel.Mediator.EntityFrameworkCore/README.md)
to the command. Authorize finalization and supply the operator pseudonym before
sending it, then authorize access to the private artifact handle before delivery.

The transaction covers revision metadata, the audit sink, and the idempotency
entry. For successful replays the receipt identifies the original committed
version and the preserved artifact handle even after a later lifecycle change.
A completed key submitted with different revision parameters or an operator
change returns a conflict. Failed attempts roll back the idempotency claim.
The audit sink receives only lineage, revision, version, operator pseudonym
and UTC time. Keep command and response objects out of structured logs.

The handler hashes the caller's opaque idempotency key before storing it. Its
idempotency scope is `document-workflow.finalize.v1` and its retention owner is
the lineage GUID in `N` format. The host owns any retention windows, legal holds,
purge authorization, and audit retention configuration.

Artifact storage happens before the relational commit. Use the immutable
artifact store contract so repeated writes of the same approved output converge
on one handle. Provide host cleanup for unreferenced output from failed attempts.
