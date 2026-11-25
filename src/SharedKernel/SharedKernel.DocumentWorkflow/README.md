# SharedKernel.DocumentWorkflow

Generates versioned structured documents from host-provided source and template ports.
A draft captures the source version, template identity and version, classified fields,
and branding. Human review uses `SharedKernel.Review` for ordered lanes and retained evidence.
The review fingerprint includes the lineage, revision number, provenance, and exact
content, so an approval cannot move between two revisions with matching field values.

Implement `IDocumentSource<TSource>` and `IDocumentTemplate<TSource>` around the
application's own models. Use `DocumentGenerator<TSource>` to generate and regenerate
drafts. The lifecycle supports starting review, recording an authorized human decision,
editing approved or pending drafts, sealing the approved revision, and voiding a revision.
An edit to an editable value requires fresh approval. Prior evidence stays readable.

Read fresh provenance from the selected source and template before approval and
finalization. Use the exact review version presented to the human reviewer.
Project the approved structured content with `ToRenderRequest()` and use the
existing `SharedKernel.DocumentRendering.IDocumentRenderer` contract. The host stores
the rendered artifact immutably and passes the resulting handle to `Seal`. A
superseded or voided finalized revision retains its original handle and structured
snapshot.

The host owns source eligibility, reviewer authorization, transaction boundaries,
and private artifact delivery. Save historical and current revisions from regeneration
in one transaction with a lineage concurrency check. The first content model covers
an ordered, classified field section with captured branding. Command integration,
artifact persistence and delivery, and optional retention and disposition holds
build on this lifecycle.

The package ID is `PandaBear.SharedKernel.DocumentWorkflow` and the namespace is
`SharedKernel.DocumentWorkflow`.

## Immutable artifact writes

`DocumentFinalizer<TSource>` checks the targeted approval and current provenance
before rendering. It rejects empty or oversized output and supplies the exact
renderer bytes and their SHA-256 to `IDocumentArtifactStore`. The configured
limit bounds accepted output. The host still bounds the input and renderer work.

Use atomic create-or-read semantics for the artifact identity. A retry of the
same lineage, revision, review version, media type and digest returns the same
private handle. An object-store adapter needs a conditional create or an equivalent
database uniqueness guarantee.

The finalizer rechecks provenance after the write and returns an immutable
snapshot to commit with the host's concurrency guard. An interrupted or stale
attempt can leave private, unreferenced content. Reconcile these objects under
the host's retention and disposition-hold rules. Keep artifact authorization
and read delivery at the host boundary.

## Persisting and restoring revisions

Map `DocumentRevision.Snapshot()` to the host's explicit storage representation.
Persist the current review version and every evidence entry in append order.
When loading a revision, reconstruct the structured content and pass the snapshot
to `DocumentRevision.Restore`. Review restore replays recorded decisions through
the same lane rules. Document restore recomputes the review fingerprint and
checks that lifecycle state, approvals, content, and the artifact handle agree.
An inconsistent row returns an invalid result before it can become an active revision.

Use a separate storage concurrency version for compare-and-set updates. Save
regeneration's historical and current snapshots atomically. The artifact
provider owns storage integrity and the host owns access authorization.
