# Payment epic #956: library slice and application handoff

## Delivery and ownership

Implement this slice in the standalone SharedKernel repository. Use the existing BuildingBlocks
package and its value-object/result conventions. Do not add dependencies, a payment framework,
parallel validator APIs, runtime reference-data downloads, or application-specific policy.

The library owns immutable `Iban`, `Bic`, `Cpf`, `Cnpj`, and explicitly typed `PixKey` values.
Factories accept canonical strings, reject invalid input through `Result<T>`, and preserve leading
zeros. Equality uses the concrete type and canonical value; Pix equality also includes the key type.
Identifiers have private constructors, read-only properties, and redacted diagnostic strings.
Validation errors do not include rejected input. The explicit `Value` property remains sensitive.
Validation proves format/check digits only, never ownership, registration, existence, or reachability.

Use SWIFT IBAN Registry release 103 (September 2026), ISO 9362 BIC syntax, BCB DICT API 2.12.1,
and Receita Federal's alphanumeric CNPJ check-digit specification. IBAN validation includes all
89 registered country layouts and MOD97-10; it does not implement national bank-specific checksum
algorithms. BIC validation checks eight/eleven-character syntax, not country assignment or a BIC
directory. Numeric and uppercase alphanumeric CNPJ use the published ASCII-minus-48 algorithm.
DICT 2.12.1 accepts alphanumeric CNPJ keys; apply the same check-digit checks as standalone CNPJ.
Pix types are selected explicitly and are never inferred. Input formatting belongs to consumers.

Do not add generated JSON/EF converters. Application adapters must call factories on materialization,
handle failures, and prevent sensitive values from entering ordinary DTOs, snapshots, logs, traces,
or exception payloads. `ToString()` redaction does not protect structured logging that reads properties.

The application already has reusable library mechanisms for encryption, idempotency, metadata-only
audit contracts, EF transactions, and PostgreSQL advisory locks. Reuse them without changes unless
a concrete generic limitation is demonstrated by a regression test.

## Application implementation order

1. Deliver #957 accounts: draft/verified/inactive revisions, independent-bank-check attestation,
   explicit Admin actions, reverification after recipient changes, masking, retirement protections,
   encrypted account details/snapshots/artifacts, and recoverable persistent key configuration.
2. Deliver #959 schedules through Tour: draft/approved/superseded EUR terms, immutable price
   snapshots, positive two-decimal installments, selected allocations, cancellation adjustments,
   business-date overdue calculations, and deletion protection.
3. Expand #959 with EUR/BRL agreed quotes, explicit expiry, settlement rounding/remainders,
   reconciliation, gross/fees/net/EUR-credit accounting, excess customer credit, refunds and full
   receipt reversals. Record transfers already performed; do not execute transfers.
4. Deliver #960 through existing document lineage, approval/finalization, HTML rendering and
   authenticated download proxy. Snapshot approved terms, selected outstanding installments,
   verified account revision, branding and quote. Recheck eligibility at finalization/download.
   Regeneration creates an immutable revision; generated financial fields are not free text.
5. Update typed account/booking/document contracts, contract-owned clients, JSON contexts,
   OpenAPI and accessible Management forms together. Keep runtime authorization and optimistic
   revision checks server-side, and use transactional financial/audit writes and keyed commands.
6. Roll out encryption configuration and additive migrations before enabling workflows. Preserve
   #1140 calendar dates; derive currency without conversion. Receipts remain unallocated until
   explicitly assigned. Keep provisional document retention and the existing production
   privacy/retention gate.

Admin inherits Operator capabilities and can finish another Admin's draft. Operators may view masked
information and record receipts; approvals and sensitive recipient access remain Admin-only.
Allow explicit self-approval, record identities/timestamps/reasons, and visibly mark it. Independent
review is optional, revision-specific and cannot be supplied by the creator. Editing invalidates
the affected review; requests for changes flag attention without silently withdrawing approval.
Never introduce staffing modes, user counts, online-presence checks or a second-person prerequisite.

## Required decisions resolved by the application plan

- Refund unallocated excess against their remaining refundable cash balance.
  Refunding excess must not reverse allocated EUR credit: a €120 receipt against €100 debt followed
  by a €20 excess refund leaves the €100 debt settled. Record append-only corrections to erroneous
  refund records; distinguish correcting a record from an actual return transfer. Block a receipt
  reversal while active refunds remain.
- Preserve historical allocations when schedules are replaced. Record explicit staff-selected
  allocation release/reassignment without double counting. Keep unapplied credit visible while
  reassignment is incomplete, and cover reduced obligations and refunds after replacement.
- Keep receipt calendar dates separate from evidenced settlement instants. Never invent midnight
  to compare a DateOnly value with quote expiry. If precise timeliness cannot be established, require
  explicit Admin reconciliation. Record quote precision, midpoint-away-from-zero rounding,
  installment remainder assignment, fees, actual settlement and FX differences independently.
- Bind financial idempotency to operation/resource and all meaningful canonical command inputs.
  Reject changed-payload replays. For instructions, include selected installments and account,
  schedule and quote revisions. Recheck outstanding amounts after payments, allocations, refunds
  and reversals; regenerate changed financial instructions. An expired quote never changes frozen
  historical terms. Pending receipts do not grant settled EUR credit.
- Protect financial history from both booking deletion and parent Tour deletion, including
  pending/cancelled bookings. Use domain guards and database constraints; existing cascade paths
  must not erase receipts, allocations, refunds, reversals or adjustments.
- Back up historical protection keys and the certificate private keys needed to unwrap them.
  Use stable application identity and separate purposes. Test restart, rotation and restoration;
  missing keys or decryption failures must fail closed. Keep financial payloads out of telemetry.
  Downloads require authorization, no-store, attachment disposition and nosniff. Already downloaded
  copies cannot be revoked.

## Validation and completion evidence

Library tests cover every registered IBAN example, checksum mutations, country structures,
canonical syntax, leading zeros, malformed/oversized inputs, Unicode lookalikes, CPF/CNPJ
checksums, all Pix types, equality/hash behavior and input-independent diagnostic strings.
Run locked restore, focused xUnit v3/MTP tests, Release build, public API checks, formatting,
generated-diagram checks and existing repository/package-consumer validation.

Application acceptance must cover the entire lifecycle with one Admin and no Operator; shared
drafts/two Admins, optional independent review, self-approval, stale revisions and concurrent edits;
server-side Operator restrictions; PostgreSQL allocation/refund/retirement races and additive
migrations; encryption recovery failures; accessible forms and end-to-end EUR/BRL workflows.
Application Aspire checks belong to the application rollout, not this pure library slice.

Keep unavailable Windows/NativeAOT evidence pending. Package publication, application candidate
adoption and remote issue changes are separate actions. Future work includes secure customer links,
automated delivery, provider FX quotes and bank reconciliation. Mandatory dual approval can only be
an optional future organizational policy.

## Sources

- [SWIFT IBAN Registry release 103](https://www.swift.com/swift-resource/9606/download).
- [ISO 9362 BIC structure](https://www.swift.com/standards/data-standards/bic-business-identifier-code).
- [BCB DICT API 2.12.1](https://www.bcb.gov.br/content/estabilidadefinanceira/pix/API-DICT.html).
- [Receita Federal CNPJ check digits](https://www.gov.br/receitafederal/pt-br/centrais-de-conteudo/publicacoes/documentos-tecnicos/cnpj/manual-dv-cnpj.pdf).
- [Microsoft Data Protection persistence and certificate rotation](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview).

Source versions checked on 2026-10-21. Update the documented version and validation examples together
when the upstream formats change.
